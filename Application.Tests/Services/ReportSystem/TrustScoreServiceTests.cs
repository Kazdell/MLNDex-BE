using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;
using Application.Services.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

using Application.Tests.Shared;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Moq;
using Infrastructure.Data;
namespace Application.Tests.Services.ReportSystem
{
  [Collection("Database collection")]
  public class TrustScoreServiceTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;
    private MlndexDbContext _db = default!;
    private int _seedTeamId;

    public TrustScoreServiceTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
      await _fixture.ResetDatabaseAsync();
      _db = _fixture.CreateDbContext();

      // Seed shared base data
      _db.Users.AddRange(
          new User { UserId = 1, Username = "user1", Email = "u1@t.com", DisplayName = "U1", PasswordHash = "h" },
          new User { UserId = 10, Username = "leader1", Email = "l1@t.com", DisplayName = "L1", PasswordHash = "h" },
          new User { UserId = 99, Username = "moderator", Email = "mod@t.com", DisplayName = "Mod", PasswordHash = "h" }
      );
      _db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
      await _db.SaveChangesAsync();

      var team = new TranslationTeam { TeamName = "Locked Team", Slug = "locked-team", TrustScore = 0, LockStatus = TeamLockStatus.LOCKED, LeaderId = 10, LanguageId = 1 };
      _db.TranslationTeams.Add(team);
      await _db.SaveChangesAsync();
      _seedTeamId = team.TeamId;
    }

    public async Task DisposeAsync()
    {
      await _db.DisposeAsync();
    }

    // ── Phase A: Admin Restore ──────────────────────────

    [Fact]
    public async Task RestoreTrustScore_ShouldIncreaseUserScore()
    {
      var db = _db;
      var user = await db.Users.FindAsync(1);
      user!.TrustScore = 0;
      user.CannotUpload = true;
      await db.SaveChangesAsync();

      var service = new TrustScoreService(db);
      var result = await service.RestoreTrustScoreAsync(new RestoreTrustScoreRequest
      {
        TargetType = TrustScoreTargetType.User,
        TargetId = 1,
        ScoreToRestore = 30,
        Reason = "Good behavior"
      }, moderatorId: 99);

      result.OldScore.Should().Be(0);
      result.NewScore.Should().Be(30);
      result.CanUpload.Should().BeTrue();

      var updatedUser = await db.Users.FindAsync(1);
      updatedUser!.CannotUpload.Should().BeFalse();
      updatedUser.TrustScore.Should().Be(30);

      var history = await db.TrustScoreHistories.FirstOrDefaultAsync(h => h.UserId == 1);
      history.Should().NotBeNull();
      history!.ScoreChange.Should().Be(30);
    }

    [Fact]
    public async Task RestoreTrustScore_ShouldCapAt100()
    {
      var db = _db;
      var user = await db.Users.FindAsync(1);
      user!.TrustScore = 80;
      await db.SaveChangesAsync();

      var service = new TrustScoreService(db);
      var result = await service.RestoreTrustScoreAsync(new RestoreTrustScoreRequest
      {
        TargetType = TrustScoreTargetType.User,
        TargetId = 1,
        ScoreToRestore = 50,
        Reason = "Restore"
      }, moderatorId: 99);

      result.NewScore.Should().Be(100); // Capped at 100
    }

    [Fact]
    public async Task RestoreTrustScore_ShouldUnlockTeam()
    {
      var db = _db;
      var service = new TrustScoreService(db);
      var result = await service.RestoreTrustScoreAsync(new RestoreTrustScoreRequest
      {
        TargetType = TrustScoreTargetType.Team,
        TargetId = _seedTeamId,
        ScoreToRestore = 20,
        Reason = "Appeal approved"
      }, moderatorId: 99);

      result.NewScore.Should().Be(20);
      result.CanUpload.Should().BeTrue();

      var team = await db.TranslationTeams.FindAsync(_seedTeamId);
      team!.LockStatus.Should().Be(TeamLockStatus.ACTIVE);
    }

    // ── Phase C: Appeal System ──────────────────────────

    [Fact]
    public async Task CreateAppeal_ShouldCreatePendingAppeal()
    {
      var db = _db;
      var service = new TrustScoreService(db);
      var result = await service.CreateAppealAsync(1, new CreateAppealRequest
      {
        Reason = "I was wrongly penalized",
        EvidenceUrl = "https://example.com/proof"
      });

      result.Status.Should().Be("Pending");
      result.Reason.Should().Be("I was wrongly penalized");
      result.UserId.Should().Be(1);
    }

    [Fact]
    public async Task CreateAppeal_ShouldReject_WhenPendingExists()
    {
      var db = _db;
      db.Appeals.Add(new Appeal { AppealId = 1, UserId = 1, Reason = "First appeal", Status = AppealStatus.Pending });
      await db.SaveChangesAsync();

      var service = new TrustScoreService(db);
      var act = () => service.CreateAppealAsync(1, new CreateAppealRequest { Reason = "Second appeal" });

      await act.Should().ThrowAsync<Application.Exceptions.AppException>()
          .WithMessage("*đã có đơn kháng cáo*");
    }

    [Fact]
    public async Task ReviewAppeal_ShouldApproveAndRestoreScore()
    {
      var db = _db;
      var user = await db.Users.FindAsync(1);
      user!.TrustScore = 10;
      user.CannotUpload = false;
      db.Appeals.Add(new Appeal { AppealId = 1, UserId = 1, Reason = "Wrong penalty", Status = AppealStatus.Pending });
      await db.SaveChangesAsync();

      var service = new TrustScoreService(db);
      var result = await service.ReviewAppealAsync(1, moderatorId: 99, new ReviewAppealRequest
      {
        IsApproved = true,
        ScoreToRestore = 40,
        ReviewNotes = "Verified innocent"
      });

      result.Status.Should().Be("Approved");
      result.ScoreRestored.Should().Be(40);

      var updatedUser = await db.Users.FindAsync(1);
      updatedUser!.TrustScore.Should().Be(50); // 10 + 40
    }

    [Fact]
    public async Task ReviewAppeal_ShouldReject()
    {
      var db = _db;
      db.Appeals.Add(new Appeal { AppealId = 1, UserId = 1, Reason = "Wrong", Status = AppealStatus.Pending });
      await db.SaveChangesAsync();

      var service = new TrustScoreService(db);
      var result = await service.ReviewAppealAsync(1, 99, new ReviewAppealRequest
      {
        IsApproved = false,
        ReviewNotes = "Evidence insufficient"
      });

      result.Status.Should().Be("Rejected");
    }

    // ── Phase D: CannotUpload ───────────────────────────

    [Fact]
    public async Task ApplyPenalty_ShouldSetCannotUpload_WhenScoreDropsToZero()
    {
      var db = _db;
      // Add a second user as violator
      db.Users.Add(new User { UserId = 2, Username = "violator", Email = "v@t.com", DisplayName = "V", PasswordHash = "h", TrustScore = 30 });
      db.Reports.Add(new Report
      {
        ReportId = 1,
        ReporterId = 1,
        ContentType = ReportTargetType.User,
        ContentId = 2,
        Status = ReportStatus.Pending,
        Reason = ReportReason.Plagiarism
      });
      await db.SaveChangesAsync();

      var mockAM = new Mock<IAccountModerationService>();
      var mockNotif = new Mock<INotificationService>();
      var reportService = new PlagiarismReportService(db, mockAM.Object, mockNotif.Object);
      await reportService.ResolveReportAsync(1, 99, new ResolvePlagiarismReportRequest
      {
        NewStatus = ReportStatus.Resolved,
        PenaltyScore = 50,
        ResolutionNotes = "Plagiarism confirmed"
      });

      var userInDb = await db.Users.FindAsync(2);
      userInDb!.TrustScore.Should().BeLessThanOrEqualTo(0);
      userInDb.CannotUpload.Should().BeTrue();
    }
  }
}
