using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Creator;
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

public class SeriesService_Manage_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IStorageService> _mockStorage = new();
  private readonly Mock<IModerationService> _mockModeration = new();
  private readonly Mock<ILogger<SeriesService>> _mockLogger = new();

  public SeriesService_Manage_CsvAlignedTests(ITestOutputHelper output)
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

  private SeriesService CreateService(MlndexDbContext db)
  {
    _mockStorage
      .Setup(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    _mockStorage
      .Setup(x => x.DeleteFolderAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    return new SeriesService(db, _mockStorage.Object, _mockModeration.Object, _mockLogger.Object);
  }

  private static async Task SeedManageBaseAsync(MlndexDbContext db)
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
        Username = "reader",
        Email = "reader@test.com",
        DisplayName = "Reader",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow.AddDays(-40)
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
        CreatedAt = DateTime.UtcNow.AddDays(-20)
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

    db.Series.AddRange(
      new Series
      {
        SeriesId = 100,
        CreatorId = 10,
        Title = "Manage Series",
        Description = "Series for manage tests",
        CoverImageUrl = "https://cdn/cover-100.jpg",
        SeriesFormat = SeriesFormat.NOVEL,
        AgeRating = AgeRating.TEEN,
        Status = SeriesStatus.ONGOING,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-25),
        UpdatedAt = DateTime.UtcNow.AddDays(-2)
      },
      new Series
      {
        SeriesId = 101,
        CreatorId = 10,
        Title = "Another Series",
        Description = "Second",
        SeriesFormat = SeriesFormat.NOVEL,
        AgeRating = AgeRating.ALL_AGES,
        Status = SeriesStatus.COMPLETED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-10)
      });

    db.Genres.AddRange(
      new Genre { GenreId = 1, Name = "Action" },
      new Genre { GenreId = 2, Name = "Drama" });

    db.SeriesGenres.AddRange(
      new SeriesGenre { SeriesGenreId = 1, SeriesId = 100, GenreId = 1 },
      new SeriesGenre { SeriesGenreId = 2, SeriesId = 100, GenreId = 2 },
      new SeriesGenre { SeriesGenreId = 3, SeriesId = 101, GenreId = 2 });

    db.Chapters.AddRange(
      new Chapter
      {
        ChapterId = 1000,
        SeriesId = 100,
        ChapterNumber = 1,
        Title = "C1",
        ContentType = ContentType.IMAGE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        LockStatus = ChapterLockStatus.FREE,
        CreatedAt = DateTime.UtcNow.AddDays(-7),
        PublishedAt = DateTime.UtcNow.AddDays(-6)
      },
      new Chapter
      {
        ChapterId = 1001,
        SeriesId = 100,
        ChapterNumber = 2,
        Title = "C2",
        ContentType = ContentType.IMAGE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        LockStatus = ChapterLockStatus.FREE,
        CreatedAt = DateTime.UtcNow.AddDays(-4),
        PublishedAt = DateTime.UtcNow.AddDays(-3)
      });

    db.ChapterPages.AddRange(
      new ChapterPage { PageId = 1, ChapterId = 1000, PageNumber = 1, ImageUrl = "https://cdn/ch1000-p1.jpg" },
      new ChapterPage { PageId = 2, ChapterId = 1001, PageNumber = 1, ImageUrl = "https://cdn/ch1001-p1.jpg" });

    db.Bookmarks.Add(new Bookmark
    {
      BookmarkId = 1,
      UserId = 2,
      SeriesId = 100,
      ChapterId = 1001,
      Note = "bookmark",
      BookmarkedAt = DateTime.UtcNow.AddDays(-1)
    });

    db.Ratings.Add(new Rating
    {
      RatingId = 1,
      UserId = 2,
      SeriesId = 100,
      Score = 5,
      Review = "great",
      CreatedAt = DateTime.UtcNow.AddDays(-1),
      UpdatedAt = DateTime.UtcNow.AddDays(-1)
    });

    db.TranslationPermissions.Add(new TranslationPermission
    {
      PermissionId = 900,
      SeriesId = 100,
      TeamId = 50,
      GrantedBy = 1,
      LanguageId = 2,
      Status = TranslationPermissionStatus.GRANTED,
      Origin = PermissionOrigin.REQUESTED_BY_TEAM,
      GrantedAt = DateTime.UtcNow.AddDays(-5)
    });

    db.ModerationQueues.AddRange(
      new ModerationQueue
      {
        QueueId = 1,
        ContentId = 1000,
        ContentType = ModerationQueueContentType.CHAPTER,
        Priority = QueuePriority.MEDIUM,
        Status = QueueStatus.PENDING,
        ReportCount = 1,
        FlaggedAt = DateTime.UtcNow.AddDays(-2)
      },
      new ModerationQueue
      {
        QueueId = 2,
        ContentId = 100,
        ContentType = ModerationQueueContentType.SERIES,
        Priority = QueuePriority.HIGH,
        Status = QueueStatus.IN_REVIEW,
        ReportCount = 1,
        FlaggedAt = DateTime.UtcNow.AddDays(-1)
      });

    db.Reports.AddRange(
      new Report
      {
        ReportId = 1,
        ReporterId = 2,
        ContentId = 1000,
        ContentType = ReportTargetType.ChapterTranslation,
        Reason = ReportReason.Other,
        Description = "Queue chapter",
        QueueId = 1,
        CreatedAt = DateTime.UtcNow.AddHours(-4)
      },
      new Report
      {
        ReportId = 2,
        ReporterId = 2,
        ContentId = 100,
        ContentType = ReportTargetType.Series,
        Reason = ReportReason.Other,
        Description = "Queue series",
        QueueId = 2,
        CreatedAt = DateTime.UtcNow.AddHours(-3)
      });

    await db.SaveChangesAsync();
  }

  private static async Task SeedDroppedOnlyAsync(MlndexDbContext db)
  {
    db.Users.Add(new User
    {
      UserId = 10,
      Username = "creator_dropped",
      Email = "drop@test.com",
      DisplayName = "Creator Dropped",
      PasswordHash = "hash",
      IsActive = true,
      IsEmailVerified = true,
      CreatedAt = DateTime.UtcNow
    });

    db.CreatorProfiles.Add(new CreatorProfile
    {
      CreatorId = 20,
      UserId = 10,
      PenName = "Drop Pen",
      IsActive = true,
      ModerationStatus = ModerationStatus.APPROVED
    });

    db.Series.Add(new Series
    {
      SeriesId = 200,
      CreatorId = 20,
      Title = "Dropped Series",
      SeriesFormat = SeriesFormat.NOVEL,
      AgeRating = AgeRating.TEEN,
      Status = SeriesStatus.DROPPED,
      ModerationStatus = ModerationStatus.APPROVED,
      CreatedAt = DateTime.UtcNow.AddDays(-2)
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetForEditAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetForEditAsync(100, 1);

    _output.WriteLine("Input: seriesId=100,userId=1(owner)");
    _output.WriteLine($"Output: title={output?.Title}, genres={output?.GenreIds.Count}, status={output?.Status}");

    output.Should().NotBeNull();
    output!.Title.Should().Be("Manage Series");
    output.GenreIds.Should().Contain(new[] { 1, 2 });
    output.Status.Should().Be("ONGOING");
  }

  [Fact]
  public async Task GetForEditAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetForEditAsync(999, 1);

    _output.WriteLine("Input: seriesId=999,userId=1");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetForEditAsync_TC03_Unauthorized()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetForEditAsync(100, 3);

    _output.WriteLine("Input: seriesId=100,userId=3(not owner)");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task UpdateStatusAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    await service.UpdateStatusAsync(100, 1, "COMPLETED", CancellationToken.None);

    var series = await db.Series.FirstAsync(s => s.SeriesId == 100);

    _output.WriteLine("Input: seriesId=100,userId=1,status=COMPLETED");
    _output.WriteLine($"Output: status={series.Status}, updatedAt={series.UpdatedAt}");

    series.Status.Should().Be(SeriesStatus.COMPLETED);
    series.UpdatedAt.Should().NotBeNull();
  }

  [Fact]
  public async Task UpdateStatusAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateStatusAsync(100, 1, "BAD_STATUS", CancellationToken.None));

    _output.WriteLine("Input: status=BAD_STATUS");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không hợp lệ");
  }

  [Fact]
  public async Task UpdateStatusAsync_TC03_Unauthorized()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UpdateStatusAsync(100, 3, "COMPLETED", CancellationToken.None));

    _output.WriteLine("Input: seriesId=100,userId=3(not owner)");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không có quyền");
  }

  [Fact]
  public async Task DeleteAsync_TC01_Success_RemoveRelatedData()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    await service.DeleteAsync(100, 1, CancellationToken.None);

    _output.WriteLine("Input: delete seriesId=100,userId=1");
    _output.WriteLine($"Output: seriesExists={await db.Series.AnyAsync(s => s.SeriesId == 100)}");

    (await db.Series.AnyAsync(s => s.SeriesId == 100)).Should().BeFalse();
    (await db.Chapters.AnyAsync(c => c.SeriesId == 100)).Should().BeFalse();
    (await db.ChapterPages.AnyAsync()).Should().BeFalse();
    (await db.Bookmarks.AnyAsync(b => b.SeriesId == 100)).Should().BeFalse();
    (await db.Ratings.AnyAsync(r => r.SeriesId == 100)).Should().BeFalse();
    (await db.ReadingHistories.AnyAsync(h => h.SeriesId == 100)).Should().BeFalse();
    (await db.TranslationPermissions.AnyAsync(p => p.SeriesId == 100)).Should().BeFalse();
    (await db.SeriesGenres.AnyAsync(sg => sg.SeriesId == 100)).Should().BeFalse();
    (await db.ModerationQueues.AnyAsync()).Should().BeFalse();
    (await db.Reports.AnyAsync()).Should().BeFalse();

    _mockStorage.Verify(x => x.DeleteAsync("https://cdn/ch1000-p1.jpg", It.IsAny<CancellationToken>()), Times.Once);
    _mockStorage.Verify(x => x.DeleteAsync("https://cdn/ch1001-p1.jpg", It.IsAny<CancellationToken>()), Times.Once);
    _mockStorage.Verify(x => x.DeleteAsync("https://cdn/cover-100.jpg", It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task DeleteAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(999, 1, CancellationToken.None));

    _output.WriteLine("Input: seriesId=999,userId=1");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không tồn tại");
  }

  [Fact]
  public async Task DeleteAsync_TC03_Unauthorized()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedManageBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(100, 3, CancellationToken.None));

    _output.WriteLine("Input: seriesId=100,userId=3(not owner)");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("không có quyền");
  }

  [Fact]
  public async Task GetRecommendationsAsync_TC01_Empty_WhenNoEligibleSeries()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedDroppedOnlyAsync(db);
    var service = CreateService(db);

    var output = await service.GetRecommendationsAsync(userId: 0, limit: 10, currentSeriesId: null);

    _output.WriteLine("Input: userId=0 with only DROPPED series in db");
    _output.WriteLine($"Output: count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetRecommendationsAsync_TC02_Success_ForAnonymousUser()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedDroppedOnlyAsync(db);
    var service = CreateService(db);

    var output = await service.GetRecommendationsAsync(userId: 0, limit: 2, currentSeriesId: null);

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetRecommendationsAsync_TC03_BusinessRule_ExcludeCurrentSeries()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedDroppedOnlyAsync(db);
    var service = CreateService(db);

    var output = await service.GetRecommendationsAsync(userId: 0, limit: 10, currentSeriesId: 100);

    output.Should().BeEmpty();
  }
}
