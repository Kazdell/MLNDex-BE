using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Creator;
using Application.Interfaces.Notification;
using Application.Services.Creator;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.CreatorServices;

public class ChapterService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IStorageService> _mockStorage = new();
  private readonly Mock<INotificationService> _mockNotification = new();
  private readonly Mock<IModerationService> _mockModeration = new();
  private readonly Mock<ILogger<ChapterService>> _mockLogger = new();

  public ChapterService_CsvAlignedTests(ITestOutputHelper output)
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

  private ChapterService CreateService(MlndexDbContext db)
  {
    _mockModeration
      .Setup(x => x.GetResultAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync((AiModerationResultDto?)null);

    _mockModeration
      .Setup(x => x.EnqueueChapterForModerationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    return new ChapterService(
      db,
      _mockStorage.Object,
      _mockLogger.Object,
      _mockNotification.Object,
      _mockModeration.Object);
  }

  private static async Task SeedChapterBaseAsync(MlndexDbContext db)
  {
    db.Users.AddRange(
      new User
      {
        UserId = 1,
        Username = "creator",
        Email = "creator@test.com",
        DisplayName = "Creator",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow.AddDays(-100)
      },
      new User
      {
        UserId = 2,
        Username = "member",
        Email = "member@test.com",
        DisplayName = "Member",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow.AddDays(-50)
      },
      new User
      {
        UserId = 3,
        Username = "outsider",
        Email = "outsider@test.com",
        DisplayName = "Outsider",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow.AddDays(-10)
      });

    db.CreatorProfiles.Add(new CreatorProfile
    {
      CreatorId = 10,
      UserId = 1,
      PenName = "Pen Creator",
      IsActive = true,
      ModerationStatus = ModerationStatus.APPROVED
    });

    db.Languages.AddRange(
      new Language { LanguageId = 1, Code = "vi", Name = "Vietnamese" },
      new Language { LanguageId = 2, Code = "en", Name = "English" });

    db.TranslationTeams.Add(new TranslationTeam
    {
      TeamId = 50,
      LeaderId = 1,
      TeamName = "Team EN",
      Slug = "team-en",
      LanguageId = 2,
      LockStatus = TeamLockStatus.ACTIVE,
      ModerationStatus = ModerationStatus.APPROVED,
      CreatedAt = DateTime.UtcNow.AddDays(-20)
    });

    db.TeamMembers.Add(new TeamMember
    {
      MembershipId = 1,
      TeamId = 50,
      UserId = 2,
      Role = TeamMemberRole.TRANSLATOR,
      JoinedAt = DateTime.UtcNow.AddDays(-5),
      IsActive = true
    });

    db.Series.Add(new Series
    {
      SeriesId = 100,
      CreatorId = 10,
      Title = "Series A",
      SeriesFormat = SeriesFormat.NOVEL,
      AgeRating = AgeRating.TEEN,
      Status = SeriesStatus.ONGOING,
      ModerationStatus = ModerationStatus.APPROVED,
      CreatedAt = DateTime.UtcNow.AddDays(-30)
    });

    db.Chapters.AddRange(
      new Chapter
      {
        ChapterId = 1000,
        SeriesId = 100,
        TeamId = null,
        LanguageId = 1,
        ChapterNumber = 1,
        Title = "Chapter 1",
        ContentType = ContentType.IMAGE,
        PageCount = 2,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        PublishedAt = DateTime.UtcNow.AddDays(-8),
        CreatedAt = DateTime.UtcNow.AddDays(-9)
      },
      new Chapter
      {
        ChapterId = 1001,
        SeriesId = 100,
        TeamId = null,
        LanguageId = 1,
        ChapterNumber = 2,
        Title = "Chapter 2",
        ContentType = ContentType.IMAGE,
        PageCount = 2,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        PublishedAt = DateTime.UtcNow.AddDays(-2),
        CreatedAt = DateTime.UtcNow.AddDays(-3),
        AiScoresJson = "{}"
      },
      new Chapter
      {
        ChapterId = 1002,
        SeriesId = 100,
        TeamId = 50,
        LanguageId = 2,
        ChapterNumber = 2,
        Title = "Chapter 2 EN",
        ContentType = ContentType.IMAGE,
        PageCount = 1,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.REJECTED,
        PublishedAt = DateTime.UtcNow.AddDays(-1),
        CreatedAt = DateTime.UtcNow.AddDays(-2)
      });

    db.ChapterPages.AddRange(
      new ChapterPage { PageId = 1, ChapterId = 1001, PageNumber = 1, ImageUrl = "https://img/1001-1.jpg" },
      new ChapterPage { PageId = 2, ChapterId = 1001, PageNumber = 2, ImageUrl = "https://img/1001-2.jpg" },
      new ChapterPage { PageId = 3, ChapterId = 1002, PageNumber = 1, ImageUrl = "https://img/1002-1.jpg" });

    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 900,
      SeriesId = 100,
      TeamId = 50,
      GrantedBy = 1,
      LanguageId = 2,
      Status = TranslationPermissionStatus.GRANTED,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM,
      GrantedAt = DateTime.UtcNow.AddDays(-6)
    });

    db.Translations.Add(new Domain.Entities.Translation
    {
      TranslationId = 7000,
      ChapterId = 1001,
      PermissionId = 900,
      LanguageId = 2,
      ContentType = ContentType.IMAGE,
      QualityStatus = TranslationQualityStatus.PUBLISHED,
      ModerationStatus = ModerationStatus.APPROVED,
      PublishedAt = DateTime.UtcNow.AddDays(-1),
      IsOfficial = true
    });

    db.TranslationPages.AddRange(
      new TranslationPage { TransPageId = 10, TranslationId = 7000, PageNumber = 1, TranslationImageUrl = "https://img/t7000-1.jpg" },
      new TranslationPage { TransPageId = 11, TranslationId = 7000, PageNumber = 2, TranslationImageUrl = "https://img/t7000-2.jpg" });

    db.Reports.Add(new Report
    {
      ReportId = 1,
      ReporterId = 2,
      ContentId = 1002,
      ContentType = ReportTargetType.ChapterTranslation,
      Reason = ReportReason.Inappropriate,
      Description = "AI_Nudity (Score: 0.95)",
      CreatedAt = DateTime.UtcNow.AddHours(-2)
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetChapterDetailAsync_TC01_Success_OriginalChapter()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetChapterDetailAsync(1001);

    _output.WriteLine("Input: chapterId=1001");
    _output.WriteLine($"Output: title={output?.Title}, prev={output?.PrevChapterId}, next={output?.NextChapterId}, pages={output?.Pages.Count}");

    output.Should().NotBeNull();
    output!.ChapterId.Should().Be(1001);
    output.SeriesTitle.Should().Be("Series A");
    output.PrevChapterId.Should().Be(1000);
    output.NextChapterId.Should().BeNull();
    output.Pages.Should().HaveCount(2);
  }

  [Fact]
  public async Task GetChapterDetailAsync_TC02_Success_FallbackTranslationId()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetChapterDetailAsync(7000);

    _output.WriteLine("Input: chapterId=7000 (translationId fallback)");
    _output.WriteLine($"Output: isTranslation={output?.IsTranslation}, chapterId={output?.ChapterId}, pages={output?.Pages.Count}, team={output?.TranslatorTeamName}");

    output.Should().NotBeNull();
    output!.IsTranslation.Should().BeTrue();
    output.ChapterId.Should().Be(7000);
    output.TranslatorTeamName.Should().Be("Team EN");
    output.Pages.Should().HaveCount(2);
  }

  [Fact]
  public async Task GetChapterDetailAsync_TC03_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetChapterDetailAsync(999999);

    _output.WriteLine("Input: chapterId=999999");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetModerationStatusAsync_TC01_Pending_WithQueuePosition()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);

    var chapter = await db.Chapters.FirstAsync(c => c.ChapterId == 1001);
    chapter.ModerationStatus = ModerationStatus.PENDING;
    await db.SaveChangesAsync();

    db.ModerationQueues.AddRange(
      new ModerationQueue
      {
        QueueId = 1,
        ContentId = 1000,
        ContentType = ModerationQueueContentType.CHAPTER,
        Priority = QueuePriority.HIGH,
        Status = QueueStatus.PENDING,
        FlaggedAt = DateTime.UtcNow.AddMinutes(-10)
      },
      new ModerationQueue
      {
        QueueId = 2,
        ContentId = 1001,
        ContentType = ModerationQueueContentType.CHAPTER,
        Priority = QueuePriority.MEDIUM,
        Status = QueueStatus.PENDING,
        FlaggedAt = DateTime.UtcNow.AddMinutes(-5)
      });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetModerationStatusAsync(1001);

    _output.WriteLine("Input: chapterId=1001 with pending queue");
    _output.WriteLine($"Output: status={output.Status}, queuePos={output.QueuePos}");

    output.Status.Should().Be("pending");
    output.QueuePos.Should().Be(2);
  }

  [Fact]
  public async Task GetModerationStatusAsync_TC02_Completed_FromResolvedQueue()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);

    db.ModerationQueues.Add(new ModerationQueue
    {
      QueueId = 3,
      ContentId = 1001,
      ContentType = ModerationQueueContentType.CHAPTER,
      Priority = QueuePriority.MEDIUM,
      Status = QueueStatus.RESOLVED,
      FlaggedAt = DateTime.UtcNow.AddMinutes(-3)
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);

    _mockModeration
      .Setup(x => x.GetResultAsync(1001, It.IsAny<CancellationToken>()))
      .ReturnsAsync(new AiModerationResultDto
      {
        Flagged = true,
        FlaggedReason = "Nudity",
        CategoryScores = new Dictionary<string, double> { ["nudity"] = 0.91 }
      });

    var output = await service.GetModerationStatusAsync(1001);

    _output.WriteLine("Input: chapterId=1001 resolved queue");
    _output.WriteLine($"Output: status={output.Status}, flagged={output.Flagged}, reason={output.FlaggedReason}");

    output.Status.Should().Be("completed");
    output.Flagged.Should().BeTrue();
    output.FlaggedReason.Should().Be("Nudity");
  }

  [Fact]
  public async Task GetModerationStatusAsync_TC03_Completed_FallbackFromChapter()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);

    // no queue for chapter 1002, but chapter status already REJECTED
    var service = CreateService(db);
    var output = await service.GetModerationStatusAsync(1002);

    _output.WriteLine("Input: chapterId=1002 no queue but chapter rejected");
    _output.WriteLine($"Output: status={output.Status}, flagged={output.Flagged}");

    output.Status.Should().Be("completed");
    output.Flagged.Should().BeTrue();
  }

  [Fact]
  public async Task GetBySeriesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetBySeriesAsync(100, 1);

    _output.WriteLine("Input: seriesId=100,userId=1(owner)");
    _output.WriteLine($"Output: count={output.Count}, firstChapterId={output.FirstOrDefault()?.ChapterId}");

    output.Should().HaveCount(3);
    output[0].ChapterNumber.Should().BeGreaterThanOrEqualTo(output[1].ChapterNumber);
  }

  [Fact]
  public async Task GetBySeriesAsync_TC02_Unauthorized()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetBySeriesAsync(100, 3));

    _output.WriteLine("Input: seriesId=100,userId=3(not owner)");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không có quyền");
  }

  [Fact]
  public async Task GetBySeriesAsync_TC03_NotFound_WhenSeriesMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var act = () => service.GetBySeriesAsync(9999, 1);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task GetTeamChaptersBySeriesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetTeamChaptersBySeriesAsync(50, 100, 2);

    _output.WriteLine("Input: teamId=50,seriesId=100,userId=2(member)");
    _output.WriteLine($"Output: count={output.Count}, first={output.FirstOrDefault()?.ChapterId}");

    output.Should().HaveCount(1);
    output[0].ChapterId.Should().Be(1002);
  }

  [Fact]
  public async Task GetTeamChaptersBySeriesAsync_TC02_Unauthorized()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetTeamChaptersBySeriesAsync(50, 100, 3));

    _output.WriteLine("Input: teamId=50,seriesId=100,userId=3(not member)");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không phải là thành viên");
  }

  [Fact]
  public async Task GetTeamChaptersBySeriesAsync_TC03_NotFound_WhenSeriesMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var act = () => service.GetTeamChaptersBySeriesAsync(50, 9999, 2);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task GetForEditAsync_TC01_Success_WithModerationReason()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetForEditAsync(1002, 1);

    _output.WriteLine("Input: chapterId=1002,userId=1(owner)");
    _output.WriteLine($"Output: moderation={output?.ModerationStatus}, reason={output?.ModerationReason}, pages={output?.Pages.Count}");

    output.Should().NotBeNull();
    output!.ModerationStatus.Should().Be("REJECTED");
    output.ModerationReason.Should().Be("Nudity");
    output.Pages.Should().HaveCount(1);
  }

  [Fact]
  public async Task GetForEditAsync_TC02_Unauthorized()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetForEditAsync(1001, 3));

    _output.WriteLine("Input: chapterId=1001,userId=3(not owner)");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không có quyền");
  }

  [Fact]
  public async Task GetForEditAsync_TC03_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedChapterBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetForEditAsync(999999, 1);

    _output.WriteLine("Input: chapterId=999999,userId=1");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }
}
