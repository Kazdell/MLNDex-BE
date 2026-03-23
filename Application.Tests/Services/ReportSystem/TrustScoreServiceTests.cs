using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;
using Application.Services.ReportSystem;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests.Services.ReportSystem
{
  public class TrustScoreServiceTests
  {
    private MlndexDbContext CreateInMemoryDbContext()
    {
      var options = new DbContextOptionsBuilder<MlndexDbContext>()
          .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
          .Options;
      return new MlndexDbContext(options);
    }

    // ── Phase A: Admin Restore ──────────────────────────

    [Fact]
    public async Task RestoreTrustScore_ShouldIncreaseUserScore()
    {
      var db = CreateInMemoryDbContext();
      db.Users.Add(new User { UserId = 1, Username = "blocked", Email = "b@t.com", DisplayName = "B", PasswordHash = "h", TrustScore = 0, CannotUpload = true });
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

      var user = await db.Users.FindAsync(1);
      user!.CannotUpload.Should().BeFalse();
      user.TrustScore.Should().Be(30);

      var history = await db.TrustScoreHistories.FirstOrDefaultAsync(h => h.UserId == 1);
      history.Should().NotBeNull();
      history!.ScoreChange.Should().Be(30);
    }

    [Fact]
    public async Task RestoreTrustScore_ShouldCapAt100()
    {
      var db = CreateInMemoryDbContext();
      db.Users.Add(new User { UserId = 1, Username = "user1", Email = "u@t.com", DisplayName = "U", PasswordHash = "h", TrustScore = 80 });
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
      var db = CreateInMemoryDbContext();
      db.TranslationTeams.Add(new TranslationTeam
      {
        TeamId = 1,
        TeamName = "Locked Team",
        Slug = "locked-team",
        TrustScore = 0,
        LockStatus = TeamLockStatus.LOCKED
      });
      await db.SaveChangesAsync();

      var service = new TrustScoreService(db);
      var result = await service.RestoreTrustScoreAsync(new RestoreTrustScoreRequest
      {
        TargetType = TrustScoreTargetType.Team,
        TargetId = 1,
        ScoreToRestore = 20,
        Reason = "Appeal approved"
      }, moderatorId: 99);

      result.NewScore.Should().Be(20);
      result.CanUpload.Should().BeTrue();

      var team = await db.TranslationTeams.FindAsync(1);
      team!.LockStatus.Should().Be(TeamLockStatus.ACTIVE);
    }

    // ── Phase C: Appeal System ──────────────────────────

    [Fact]
    public async Task CreateAppeal_ShouldCreatePendingAppeal()
    {
      var db = CreateInMemoryDbContext();
      db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@t.com", DisplayName = "A", PasswordHash = "h" });
      await db.SaveChangesAsync();

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
      var db = CreateInMemoryDbContext();
      db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@t.com", DisplayName = "A", PasswordHash = "h" });
      db.Appeals.Add(new Appeal { AppealId = 1, UserId = 1, Reason = "First appeal", Status = AppealStatus.Pending });
      await db.SaveChangesAsync();

      var service = new TrustScoreService(db);
      var act = () => service.CreateAppealAsync(1, new CreateAppealRequest { Reason = "Second appeal" });

      await act.Should().ThrowAsync<InvalidOperationException>()
          .WithMessage("*đã có đơn kháng cáo*");
    }

    [Fact]
    public async Task ReviewAppeal_ShouldApproveAndRestoreScore()
    {
      var db = CreateInMemoryDbContext();
      db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@t.com", DisplayName = "A", PasswordHash = "h", TrustScore = 10, CannotUpload = false });
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

      var user = await db.Users.FindAsync(1);
      user!.TrustScore.Should().Be(50); // 10 + 40
    }

    [Fact]
    public async Task ReviewAppeal_ShouldReject()
    {
      var db = CreateInMemoryDbContext();
      db.Users.Add(new User { UserId = 1, Username = "appealer", Email = "a@t.com", DisplayName = "A", PasswordHash = "h" });
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
      var db = CreateInMemoryDbContext();
      db.Users.Add(new User { UserId = 1, Username = "reporter", Email = "r@t.com", DisplayName = "R", PasswordHash = "h" });
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

      var reportService = new PlagiarismReportService(db);
      await reportService.ResolveReportAsync(1, 99, new ResolvePlagiarismReportRequest
      {
        NewStatus = ReportStatus.Resolved,
        PenaltyScore = 50,
        ResolutionNotes = "Plagiarism confirmed"
      });

      var user = await db.Users.FindAsync(2);
      user!.TrustScore.Should().BeLessThanOrEqualTo(0);
      user.CannotUpload.Should().BeTrue();
    }
  }
}
