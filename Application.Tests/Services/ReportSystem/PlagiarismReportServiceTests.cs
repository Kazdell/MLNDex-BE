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
  public class PlagiarismReportServiceTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;
    private MlndexDbContext _db = default!;
    private int _seedTeamId;

    public PlagiarismReportServiceTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
      await _fixture.ResetDatabaseAsync();
      _db = _fixture.CreateDbContext();

      // Seed shared base data that satisfies all FK constraints
      _db.Users.AddRange(
          new User { UserId = 1, Username = "reporter1", Email = "reporter1@gmail.com", DisplayName = "R1", PasswordHash = "hash" },
          new User { UserId = 2, Username = "baduser", Email = "baduser@test.com", DisplayName = "B1", PasswordHash = "hash" },
          new User { UserId = 3, Username = "moderator1", Email = "mod1@test.com", DisplayName = "M1", PasswordHash = "hash" },
          new User { UserId = 10, Username = "leader1", Email = "leader1@test.com", DisplayName = "L1", PasswordHash = "hash" }
      );
      _db.Languages.Add(new Language { LanguageId = 1, Name = "Vietnamese", Code = "vi" });
      _db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 1, UserId = 1, PenName = "Creator1", ReputationScore = 100 });
      _db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 2, UserId = 2, PenName = "BadCreator", ReputationScore = 100 });
      await _db.SaveChangesAsync();

      _db.Series.Add(new Series { SeriesId = 10, Title = "Test Series", CreatorId = 1 });
      var team = new TranslationTeam { TeamName = "Team A", Slug = "team-a", ReputationScore = 100, LeaderId = 10, LanguageId = 1 };
      _db.TranslationTeams.Add(team);
      await _db.SaveChangesAsync();
      _seedTeamId = team.TeamId;

      _db.Chapters.Add(new Chapter { ChapterId = 1, SeriesId = 10, ChapterNumber = 1 });
      await _db.SaveChangesAsync();

      _db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 1, SeriesId = 10, TeamId = _seedTeamId, LanguageId = 1, GrantedBy = 1 });
      await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
      await _db.DisposeAsync();
    }

    [Fact]
    public async Task CreateReportAsync_ShouldCreatePendingReport_WhenUserExists()
    {
      var db = _db;
      var mockAM = new Mock<IAccountModerationService>();
      var mockNotif = new Mock<INotificationService>();
      var mockPusher = new Mock<INotificationPusher>();
      var service = new PlagiarismReportService(db, mockAM.Object, mockNotif.Object, mockPusher.Object);
      var request = new CreatePlagiarismReportRequest
      {
        TargetType = ReportTargetType.Series,
        TargetId = 10,
        Reason = ReportReason.Plagiarism,
        Description = "This is copied"
      };

      var result = await service.CreateReportAsync(1, request);

      result.Should().NotBeNull();
      result.Status.Should().Be(ReportStatus.Pending);
      result.TargetType.Should().Be(ReportTargetType.Series);

      var reportInDb = await db.Reports.FirstOrDefaultAsync(r => r.ReportId == result.ReportId);
      reportInDb.Should().NotBeNull();
      reportInDb?.ContentType.Should().Be(ReportTargetType.Series);
    }

    [Fact]
    public async Task ResolveReportAsync_ShouldApplyPenaltyToTeam_WhenTargetIsTeam()
    {
      var db = _db;
      db.Reports.Add(new Report
      {
        ReportId = 100,
        ReporterId = 1,
        ContentType = ReportTargetType.Team,
        ContentId = _seedTeamId,
        Status = ReportStatus.Pending,
        Reason = ReportReason.Plagiarism
      });
      await db.SaveChangesAsync();

      var mockAM = new Mock<IAccountModerationService>();
      var mockNotif = new Mock<INotificationService>();
      var mockPusher = new Mock<INotificationPusher>();
      var service = new PlagiarismReportService(db, mockAM.Object, mockNotif.Object, mockPusher.Object);
      var request = new ResolvePlagiarismReportRequest
      {
        NewStatus = ReportStatus.Resolved,
        PenaltyScore = 50,
        ResolutionNotes = "Plagiarism confirmed"
      };

      var result = await service.ResolveReportAsync(100, 2, request);

      result.Status.Should().Be(ReportStatus.Resolved);

      var teamInDb = await db.TranslationTeams.FindAsync(_seedTeamId);
      teamInDb?.ReputationScore.Should().Be(50); // 100 - 50

      var history = await db.ReputationHistories.FirstOrDefaultAsync(h => h.TranslationTeamId == _seedTeamId);
      history.Should().NotBeNull();
      history?.ScoreChange.Should().Be(-50);
      history?.RelatedReportId.Should().Be(100);
    }

    [Fact]
    public async Task ResolveReportAsync_ShouldApplyPenaltyToUser_WhenTargetIsUser()
    {
      var db = _db;
      db.Reports.Add(new Report
      {
        ReportId = 101,
        ReporterId = 1,
        ContentType = ReportTargetType.User,
        ContentId = 2,
        Status = ReportStatus.Pending,
        Reason = ReportReason.Other
      });
      await db.SaveChangesAsync();

      var mockAM = new Mock<IAccountModerationService>();
      var mockNotif = new Mock<INotificationService>();
      var mockPusher = new Mock<INotificationPusher>();
      var service = new PlagiarismReportService(db, mockAM.Object, mockNotif.Object, mockPusher.Object);
      var request = new ResolvePlagiarismReportRequest
      {
        NewStatus = ReportStatus.Resolved,
        PenaltyScore = 20,
        ResolutionNotes = "Spamming confirmed"
      };

      var result = await service.ResolveReportAsync(101, 3, request);

      result.Status.Should().Be(ReportStatus.Resolved);

      var creatorInDb = await db.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == 2);
      creatorInDb?.ReputationScore.Should().Be(80); // 100 - 20

      var history = await db.ReputationHistories.FirstOrDefaultAsync(h => h.CreatorId == creatorInDb.CreatorId);
      history.Should().NotBeNull();
      history?.ScoreChange.Should().Be(-20);
      history?.RelatedReportId.Should().Be(101);
    }

    [Fact]
    public async Task ResolveReportAsync_ShouldStrikeContent_WhenStrikeIsTrue()
    {
      var db = _db;
      db.Translations.Add(new Domain.Entities.Translation { TranslationId = 10, ModerationStatus = ModerationStatus.APPROVED, LanguageId = 1, ChapterId = 1, PermissionId = 1 });
      db.Reports.Add(new Report
      {
        ReportId = 102,
        ReporterId = 1,
        ContentType = ReportTargetType.ChapterTranslation,
        ContentId = 10,
        Status = ReportStatus.Investigating,
        Reason = ReportReason.Plagiarism
      });
      await db.SaveChangesAsync();

      var mockAM = new Mock<IAccountModerationService>();
      var mockNotif = new Mock<INotificationService>();
      var mockPusher = new Mock<INotificationPusher>();
      var service = new PlagiarismReportService(db, mockAM.Object, mockNotif.Object, mockPusher.Object);
      var request = new ResolvePlagiarismReportRequest
      {
        NewStatus = ReportStatus.Resolved,
        StrikeContent = true,
        ResolutionNotes = "Struck content"
      };

      var result = await service.ResolveReportAsync(102, 2, request);

      var transInDb = await db.Translations.FindAsync(10);
      transInDb?.ModerationStatus.Should().Be(ModerationStatus.REJECTED);
    }
  }
}
