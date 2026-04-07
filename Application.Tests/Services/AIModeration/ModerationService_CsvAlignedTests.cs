using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.AIModeration;
using Application.DTOs.Moderation;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Creator;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Application.Interfaces.Queue;
using Application.Services.AIModeration;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.AIModeration;

public class ModerationService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IAiModerationClient> _mockAiClient = new();
  private readonly Mock<IBlacklistProvider> _mockBlacklist = new();
  private readonly Mock<IOCRService> _mockOcr = new();
  private readonly Mock<IModerationQueue> _mockQueue = new();
  private readonly Mock<INotificationService> _mockNotification = new();
  private readonly Mock<IStorageService> _mockStorage = new();
  private readonly Mock<ILogger<ModerationService>> _mockLogger = new();

  public ModerationService_CsvAlignedTests(ITestOutputHelper output)
  {
    _output = output;
  }

  private static MlndexDbContext CreateInMemoryDbContext()
  {
    var options = new DbContextOptionsBuilder<MlndexDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;
    return new MlndexDbContext(options);
  }

  private ModerationService CreateService(MlndexDbContext db)
  {
    _mockAiClient
      .Setup(x => x.ModerateImagesAsync(It.IsAny<IEnumerable<string>>()))
      .ReturnsAsync(new AiModerationResultDto
      {
        Flagged = false,
        CategoryScores = new Dictionary<string, double>()
      });

    _mockBlacklist.SetupGet(x => x.ProfanityList).Returns(new List<BlacklistEntry>());
    _mockBlacklist.SetupGet(x => x.HateSpeechList).Returns(new List<BlacklistEntry>());
    _mockBlacklist.SetupGet(x => x.IllegalContentList).Returns(new List<BlacklistEntry>());
    _mockBlacklist.SetupGet(x => x.RejectionTemplates).Returns(new List<RejectionTemplateDto>());
    _mockBlacklist.SetupGet(x => x.BannedTags).Returns(new List<BannedTagDto>());
    _mockBlacklist.SetupGet(x => x.RestrictedTags).Returns(new List<BannedTagDto>());
    _mockBlacklist.SetupGet(x => x.Thresholds).Returns(new Dictionary<string, ThresholdRule>());

    _mockOcr
      .Setup(x => x.ExtractTextFromImageAsync(It.IsAny<byte[]>()))
      .ReturnsAsync(string.Empty);

    _mockQueue
      .Setup(x => x.EnqueueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .Returns(ValueTask.CompletedTask);

    _mockNotification
      .Setup(x => x.CreateNotificationAsync(
        It.IsAny<int>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<NotificationType>()))
      .ReturnsAsync(new Application.DTOs.Notification.NotificationDto());

    return new ModerationService(
      db,
      _mockAiClient.Object,
      _mockLogger.Object,
      _mockBlacklist.Object,
      _mockOcr.Object,
      _mockQueue.Object,
      _mockNotification.Object,
      _mockStorage.Object);
  }

  private static async Task SeedAsync(MlndexDbContext db)
  {
    db.Users.Add(new User { UserId = 1, Username = "creator_user", Email = "creator@test.com", DisplayName = "Creator User", PasswordHash = "hash" });
    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 10, UserId = 1, PenName = "Pen One", ReputationScore = 100 });
    db.Languages.Add(new Language { LanguageId = 1, Code = "vi", Name = "Vietnamese" });
    db.Series.Add(new Series
    {
      SeriesId = 100,
      CreatorId = 10,
      Title = "Series A",
      Description = "normal description",
      AgeRating = AgeRating.TEEN,
      CoverImageUrl = "https://img/cover.jpg"
    });

    db.Chapters.Add(new Chapter
    {
      ChapterId = 200,
      SeriesId = 100,
      ChapterNumber = 1,
      Title = "Chapter A",
      ContentType = ContentType.IMAGE,
      Status = ChapterStatus.DRAFT,
      ModerationStatus = ModerationStatus.PENDING,
      LockStatus = ChapterLockStatus.FREE,
      LanguageId = 1,
      CreatedAt = DateTime.UtcNow
    });

    db.ChapterPages.Add(new ChapterPage { PageId = 1, ChapterId = 200, PageNumber = 1, ImageUrl = "https://img/chapter-page.jpg" });

    db.TranslationTeams.Add(new TranslationTeam { TeamId = 50, LeaderId = 1, TeamName = "Team A", Slug = "team-a", LanguageId = 1 });
    db.TeamMembers.Add(new TeamMember { MembershipId = 1, TeamId = 50, UserId = 1, Role = TeamMemberRole.LEADER, IsActive = true, JoinedAt = DateTime.UtcNow });
    db.TranslationPermissions.Add(new TranslationPermission { PermissionId = 500, TeamId = 50, SeriesId = 100, LanguageId = 1, Status = TranslationPermissionStatus.GRANTED, Origin = PermissionOrigin.REQUESTED_BY_TEAM, GrantedBy = 1 });

    db.Translations.Add(new Domain.Entities.Translation
    {
      TranslationId = 300,
      ChapterId = 200,
      PermissionId = 500,
      LanguageId = 1,
      ContentType = ContentType.IMAGE,
      ModerationStatus = ModerationStatus.PENDING,
      QualityStatus = TranslationQualityStatus.DRAFT
    });
    db.TranslationPages.Add(new TranslationPage { TransPageId = 1, TranslationId = 300, PageNumber = 1, TranslationImageUrl = "https://img/translation-page.jpg" });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task RunAiModerationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var output = await service.RunAiModerationAsync(200);

    _output.WriteLine("Input: chapterId=200 with one image");
    _output.WriteLine($"Output: flagged={output.Flagged}, chapterStatus={db.Chapters.Find(200)!.ModerationStatus}");

    db.Chapters.Find(200)!.ModerationStatus.Should().Be(ModerationStatus.APPROVED);
  }

  [Fact]
  public async Task RunAiModerationAsync_TC02_NotFound_WhenChapterMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var act = () => service.RunAiModerationAsync(9999);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task RunAiModerationAsync_TC03_BusinessRule_TitleViolationAutoRejects()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);
    _mockBlacklist.SetupGet(x => x.IllegalContentList).Returns(new List<BlacklistEntry>
    {
      new() { Word = "bannedword", Severity = "extreme", Variants = new List<string>() }
    });

    var chapter = await db.Chapters.FirstAsync(c => c.ChapterId == 200);
    chapter.Title = "contains bannedword";
    await db.SaveChangesAsync();

    var output = await service.RunAiModerationAsync(200);

    output.Flagged.Should().BeTrue();
    output.FlaggedReason.Should().Be("title_violation");
    (await db.Chapters.FirstAsync(c => c.ChapterId == 200)).ModerationStatus.Should().Be(ModerationStatus.REJECTED);
  }

  [Fact]
  public async Task RunSeriesModerationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var output = await service.RunSeriesModerationAsync(100);

    _output.WriteLine("Input: seriesId=100");
    _output.WriteLine($"Output: flagged={output.Flagged}, moderationStatus={db.Series.Find(100)!.ModerationStatus}");

    db.Series.Find(100)!.ModerationStatus.Should().Be(ModerationStatus.APPROVED);
  }

  [Fact]
  public async Task RunSeriesModerationAsync_TC02_NotFound_WhenSeriesMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var act = () => service.RunSeriesModerationAsync(9999);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task RunSeriesModerationAsync_TC03_BusinessRule_TitleViolationAutoRejects()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);
    _mockBlacklist.SetupGet(x => x.IllegalContentList).Returns(new List<BlacklistEntry>
    {
      new() { Word = "seriesban", Severity = "extreme", Variants = new List<string>() }
    });

    var series = await db.Series.FirstAsync(s => s.SeriesId == 100);
    series.Title = "contains seriesban";
    await db.SaveChangesAsync();

    var output = await service.RunSeriesModerationAsync(100);

    output.Flagged.Should().BeTrue();
    output.FlaggedReason.Should().Be("title_violation");
    (await db.Series.FirstAsync(s => s.SeriesId == 100)).ModerationStatus.Should().Be(ModerationStatus.REJECTED);
  }

  [Fact]
  public async Task RunTranslationModerationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var output = await service.RunTranslationModerationAsync(300);

    _output.WriteLine("Input: translationId=300 image type");
    _output.WriteLine($"Output: flagged={output.Flagged}, moderationStatus={db.Translations.Find(300)!.ModerationStatus}");

    db.Translations.Find(300)!.ModerationStatus.Should().Be(ModerationStatus.APPROVED);
  }

  [Fact]
  public async Task RunTranslationModerationAsync_TC02_NotFound_WhenTranslationMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var act = () => service.RunTranslationModerationAsync(9999);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task RunTranslationModerationAsync_TC03_InvalidInput_WhenAiFlagsContent()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);
    _mockAiClient
      .Setup(x => x.ModerateImagesAsync(It.IsAny<IEnumerable<string>>()))
      .ReturnsAsync(new AiModerationResultDto
      {
        Flagged = true,
        FlaggedReason = "unsafe-image",
        CategoryScores = new Dictionary<string, double>()
      });

    var output = await service.RunTranslationModerationAsync(300);

    output.Flagged.Should().BeTrue();
    (await db.Translations.FirstAsync(t => t.TranslationId == 300)).ModerationStatus.Should().Be(ModerationStatus.REJECTED);
  }

  [Fact]
  public async Task GetResultAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var chapter = await db.Chapters.FirstAsync(c => c.ChapterId == 200);
    chapter.ModerationStatus = ModerationStatus.APPROVED;
    chapter.AiScoresJson = "{\"categoryScores\":{\"violence\":0.1}}";
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetResultAsync(200);

    _output.WriteLine("Input: approved chapter with AiScoresJson");
    _output.WriteLine($"Output: flagged={output!.Flagged}, scoreCount={output.CategoryScores.Count}");

    output.Should().NotBeNull();
    output!.Flagged.Should().BeFalse();
    output.CategoryScores.Should().ContainKey("violence");
  }

  [Fact]
  public async Task GetResultAsync_TC02_NotFound_WhenChapterMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var output = await service.GetResultAsync(9999);

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetResultAsync_TC03_BusinessRule_ReturnsLatestAiReasonForRejectedChapter()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var chapter = await db.Chapters.FirstAsync(c => c.ChapterId == 200);
    chapter.ModerationStatus = ModerationStatus.REJECTED;
    chapter.AiScoresJson = "{\"categoryScores\":{\"hate\":0.82}}";
    db.Reports.Add(new Report
    {
      ReporterId = 1,
      ContentId = 200,
      ContentType = ReportTargetType.ChapterTranslation,
      Reason = ReportReason.Inappropriate,
      Description = "AI_hate (Score: 0.82)",
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetResultAsync(200);

    output.Should().NotBeNull();
    output!.Flagged.Should().BeTrue();
    output.FlaggedReason.Should().Be("hate");
  }

  [Fact]
  public async Task SubmitAppealAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var chapter = await db.Chapters.FirstAsync(c => c.ChapterId == 200);
    chapter.ModerationStatus = ModerationStatus.REJECTED;
    await db.SaveChangesAsync();

    var service = CreateService(db);
    await service.SubmitAppealAsync(200, 1, "please review");

    var queue = await db.ModerationQueues.FirstOrDefaultAsync(q => q.ContentId == 200 && q.ContentType == ModerationQueueContentType.CHAPTER);

    _output.WriteLine("Input: rejected chapter submit appeal");
    _output.WriteLine($"Output: queueStatus={queue!.Status}, appealCount={queue.AppealCount}");

    queue.Should().NotBeNull();
    queue!.Status.Should().Be(QueueStatus.PENDING);
    queue.AppealCount.Should().BeGreaterThan(0);
  }

  [Fact]
  public async Task SubmitAppealAsync_TC02_NotFound_WhenChapterMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    var act = () => service.SubmitAppealAsync(9999, 1, "appeal");

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task SubmitAppealAsync_TC03_InvalidInput_WhenChapterNotRejected()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var chapter = await db.Chapters.FirstAsync(c => c.ChapterId == 200);
    chapter.ModerationStatus = ModerationStatus.APPROVED;
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var act = () => service.SubmitAppealAsync(200, 1, "appeal");

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task EnqueueChapterForModerationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    await service.EnqueueChapterForModerationAsync(200);

    var queue = await db.ModerationQueues.FirstOrDefaultAsync(q => q.ContentId == 200 && q.ContentType == ModerationQueueContentType.CHAPTER);

    _output.WriteLine("Input: enqueue chapterId=200");
    _output.WriteLine($"Output: queueCreated={queue != null}, priority={queue?.Priority}");

    queue.Should().NotBeNull();
    _mockQueue.Verify(x => x.EnqueueAsync(200, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task EnqueueChapterForModerationAsync_TC02_BusinessRule_ReplacesExistingQueueAndRelations()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var oldQueue = new ModerationQueue
    {
      QueueId = 999,
      ContentId = 200,
      ContentType = ModerationQueueContentType.CHAPTER,
      Priority = QueuePriority.MEDIUM,
      Status = QueueStatus.IN_REVIEW,
      FlaggedAt = DateTime.UtcNow.AddHours(-1),
      ReportCount = 1
    };
    db.ModerationQueues.Add(oldQueue);
    db.Reports.Add(new Report
    {
      ReporterId = 1,
      ContentId = 200,
      ContentType = ReportTargetType.ChapterTranslation,
      Reason = ReportReason.Inappropriate,
      Description = "old report",
      QueueId = 999,
      CreatedAt = DateTime.UtcNow
    });
    db.ModerationActions.Add(new ModerationAction { QueueId = 999, ModeratorId = 1, Action = ModerationActionType.FlagForReview, Reason = "old", ActedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    await service.EnqueueChapterForModerationAsync(200);

    (await db.ModerationQueues.CountAsync(q => q.ContentId == 200 && q.ContentType == ModerationQueueContentType.CHAPTER)).Should().Be(1);
    (await db.Reports.AnyAsync(r => r.QueueId == 999)).Should().BeFalse();
    (await db.ModerationActions.AnyAsync(a => a.QueueId == 999)).Should().BeFalse();
  }

  [Fact]
  public async Task EnqueueChapterForModerationAsync_TC03_BusinessRule_SendsCreatorNotification()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    await service.EnqueueChapterForModerationAsync(200);

    _mockNotification.Verify(x => x.CreateNotificationAsync(
      1,
      It.IsAny<string>(),
      It.IsAny<string>(),
      It.IsAny<string>(),
      NotificationType.SYSTEM), Times.Once);
  }

  [Fact]
  public async Task EnqueueSeriesForModerationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    await service.EnqueueSeriesForModerationAsync(100);

    var queue = await db.ModerationQueues.FirstOrDefaultAsync(q => q.ContentId == 100 && q.ContentType == ModerationQueueContentType.SERIES);

    _output.WriteLine("Input: enqueue seriesId=100");
    _output.WriteLine($"Output: queueCreated={queue != null}, status={queue?.Status}");

    queue.Should().NotBeNull();
    _mockQueue.Verify(x => x.EnqueueAsync(100, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task EnqueueSeriesForModerationAsync_TC02_BusinessRule_CreatesPendingHighPriorityQueue()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    await service.EnqueueSeriesForModerationAsync(100);

    var queue = await db.ModerationQueues.FirstAsync(q => q.ContentId == 100 && q.ContentType == ModerationQueueContentType.SERIES);
    queue.Priority.Should().Be(QueuePriority.HIGH);
    queue.Status.Should().Be(QueueStatus.PENDING);
  }

  [Fact]
  public async Task EnqueueSeriesForModerationAsync_TC03_SpecialCase_DuplicateEnqueueCreatesAnotherRecord()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    await service.EnqueueSeriesForModerationAsync(100);
    await service.EnqueueSeriesForModerationAsync(100);

    (await db.ModerationQueues.CountAsync(q => q.ContentId == 100 && q.ContentType == ModerationQueueContentType.SERIES)).Should().Be(2);
  }

  [Fact]
  public async Task EnqueueTranslationForModerationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    await service.EnqueueTranslationForModerationAsync(300);

    var queue = await db.ModerationQueues.FirstOrDefaultAsync(q => q.ContentId == 300 && q.ContentType == ModerationQueueContentType.TRANSLATION);

    _output.WriteLine("Input: enqueue translationId=300");
    _output.WriteLine($"Output: queueCreated={queue != null}, status={queue?.Status}");

    queue.Should().NotBeNull();
    _mockQueue.Verify(x => x.EnqueueAsync(300, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task EnqueueTranslationForModerationAsync_TC02_BusinessRule_ReplacesExistingQueueAndRelations()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var oldQueue = new ModerationQueue
    {
      QueueId = 1000,
      ContentId = 300,
      ContentType = ModerationQueueContentType.TRANSLATION,
      Priority = QueuePriority.MEDIUM,
      Status = QueueStatus.IN_REVIEW,
      FlaggedAt = DateTime.UtcNow.AddHours(-1),
      ReportCount = 1
    };
    db.ModerationQueues.Add(oldQueue);
    db.Reports.Add(new Report
    {
      ReporterId = 1,
      ContentId = 300,
      ContentType = ReportTargetType.ChapterTranslation,
      Reason = ReportReason.Inappropriate,
      Description = "old translation report",
      QueueId = 1000,
      CreatedAt = DateTime.UtcNow
    });
    db.ModerationActions.Add(new ModerationAction { QueueId = 1000, ModeratorId = 1, Action = ModerationActionType.FlagForReview, Reason = "old", ActedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    await service.EnqueueTranslationForModerationAsync(300);

    (await db.ModerationQueues.CountAsync(q => q.ContentId == 300 && q.ContentType == ModerationQueueContentType.TRANSLATION)).Should().Be(1);
    (await db.Reports.AnyAsync(r => r.QueueId == 1000)).Should().BeFalse();
    (await db.ModerationActions.AnyAsync(a => a.QueueId == 1000)).Should().BeFalse();
  }

  [Fact]
  public async Task EnqueueTranslationForModerationAsync_TC03_BusinessRule_SendsNotificationToTeamLeader()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = CreateService(db);

    await service.EnqueueTranslationForModerationAsync(300);

    _mockNotification.Verify(x => x.CreateNotificationAsync(
      1,
      It.IsAny<string>(),
      It.IsAny<string>(),
      It.IsAny<string>(),
      NotificationType.SYSTEM), Times.Once);
  }
}
