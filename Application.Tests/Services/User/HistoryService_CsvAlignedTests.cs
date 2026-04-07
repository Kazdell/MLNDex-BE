using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.User;
using Application.Services.User;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.UserServices;

public class HistoryService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public HistoryService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedHistoryBaseAsync(MlndexDbContext db)
  {
    db.Users.AddRange(
      new User
      {
        UserId = 1,
        Username = "reader1",
        Email = "reader1@test.com",
        DisplayName = "Reader 1",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow
      },
      new User
      {
        UserId = 2,
        Username = "reader2",
        Email = "reader2@test.com",
        DisplayName = "Reader 2",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow
      },
      new User
      {
        UserId = 100,
        Username = "creator_owner",
        Email = "creator@test.com",
        DisplayName = "Creator",
        PasswordHash = "hash",
        IsActive = true,
        IsEmailVerified = true,
        CreatedAt = DateTime.UtcNow
      });

    db.CreatorProfiles.Add(new CreatorProfile
    {
      CreatorId = 200,
      UserId = 100,
      PenName = "CreatorPen",
      IsActive = true,
      ModerationStatus = ModerationStatus.APPROVED
    });

    db.Series.AddRange(
      new Series
      {
        SeriesId = 10,
        CreatorId = 200,
        Title = "Series A",
        CoverImageUrl = "https://img/a.jpg",
        Status = SeriesStatus.ONGOING,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-20)
      },
      new Series
      {
        SeriesId = 11,
        CreatorId = 200,
        Title = "Series B",
        CoverImageUrl = "https://img/b.jpg",
        Status = SeriesStatus.ONGOING,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-10)
      });

    db.Chapters.AddRange(
      new Chapter
      {
        ChapterId = 1000,
        SeriesId = 10,
        ChapterNumber = 1,
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-8)
      },
      new Chapter
      {
        ChapterId = 1001,
        SeriesId = 10,
        ChapterNumber = 2,
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-2)
      },
      new Chapter
      {
        ChapterId = 1100,
        SeriesId = 11,
        ChapterNumber = 1,
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-1)
      });

    db.Translations.Add(new Domain.Entities.Translation
    {
      TranslationId = 7000,
      ChapterId = 1001,
      PermissionId = 1,
      ContentType = ContentType.TEXT,
      QualityStatus = TranslationQualityStatus.PUBLISHED,
      ModerationStatus = ModerationStatus.APPROVED
    });

    db.Genres.AddRange(
      new Genre { GenreId = 1, Name = "Action" },
      new Genre { GenreId = 2, Name = "Drama" });

    db.SeriesGenres.AddRange(
      new SeriesGenre { SeriesGenreId = 1, SeriesId = 10, GenreId = 1 },
      new SeriesGenre { SeriesGenreId = 2, SeriesId = 11, GenreId = 2 });

    db.Follows.Add(new Follow
    {
      FollowId = 900,
      UserId = 1,
      TargetId = 10,
      TargetType = FollowTargetType.SERIES,
      FollowedAt = DateTime.UtcNow.AddDays(-3)
    });

    db.Ratings.Add(new Rating
    {
      RatingId = 910,
      UserId = 1,
      SeriesId = 10,
      Score = 5,
      Review = "Great",
      CreatedAt = DateTime.UtcNow.AddDays(-2),
      UpdatedAt = DateTime.UtcNow.AddDays(-2)
    });

    db.Bookmarks.Add(new Bookmark
    {
      BookmarkId = 920,
      UserId = 1,
      SeriesId = 10,
      ChapterId = 1001,
      Note = "bookmark",
      BookmarkedAt = DateTime.UtcNow.AddDays(-1)
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task UpdateHistoryAsync_TC01_Success_WithChapterId()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    var service = new HistoryService(db);
    var input = new ReadingHistoryUpdateDto
    {
      SeriesId = 10,
      ChapterId = 1000,
      PageNumber = 12
    };

    var output = await service.UpdateHistoryAsync(1, input);

    _output.WriteLine($"Input: userId=1, seriesId={input.SeriesId}, chapterId={input.ChapterId}, page={input.PageNumber}");
    _output.WriteLine($"Output: updated={output}");

    output.Should().BeTrue();

    var history = await db.ReadingHistories.FirstOrDefaultAsync(h => h.UserId == 1 && h.SeriesId == 10);
    history.Should().NotBeNull();
    history!.LastChapterId.Should().Be(1000);
    history.LastPageNumber.Should().Be(12);
  }

  [Fact]
  public async Task UpdateHistoryAsync_TC02_BusinessRule_ResolveFromTranslationId()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    var service = new HistoryService(db);
    var output = await service.UpdateHistoryAsync(1, new ReadingHistoryUpdateDto
    {
      SeriesId = 10,
      ChapterId = 7000,
      PageNumber = 19
    });

    _output.WriteLine("Input: chapterId is translationId=7000");
    _output.WriteLine($"Output: updated={output}");

    output.Should().BeTrue();

    var history = await db.ReadingHistories.FirstAsync(h => h.UserId == 1 && h.SeriesId == 10);
    history.LastChapterId.Should().Be(1001);
    history.LastPageNumber.Should().Be(19);
  }

  [Fact]
  public async Task UpdateHistoryAsync_TC03_InvalidInput_NotFoundChapterOrTranslation()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    var service = new HistoryService(db);
    var output = await service.UpdateHistoryAsync(1, new ReadingHistoryUpdateDto
    {
      SeriesId = 10,
      ChapterId = 999999,
      PageNumber = 1
    });

    _output.WriteLine("Input: invalid chapterId/translationId=999999");
    _output.WriteLine($"Output: updated={output}");

    output.Should().BeFalse();
    (await db.ReadingHistories.AnyAsync(h => h.UserId == 1 && h.SeriesId == 10)).Should().BeFalse();
  }

  [Fact]
  public async Task GetUserHistoryAsync_TC01_Success_OrderByLastReadDesc()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    db.ReadingHistories.AddRange(
      new ReadingHistory
      {
        HistoryId = 100,
        UserId = 1,
        SeriesId = 10,
        LastChapterId = 1000,
        LastPageNumber = 5,
        LastReadAt = DateTime.UtcNow.AddHours(-5)
      },
      new ReadingHistory
      {
        HistoryId = 101,
        UserId = 1,
        SeriesId = 11,
        LastChapterId = 1100,
        LastPageNumber = 7,
        LastReadAt = DateTime.UtcNow.AddHours(-1)
      });
    await db.SaveChangesAsync();

    var service = new HistoryService(db);
    var output = await service.GetUserHistoryAsync(1);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: count={output.Count}, firstSeriesId={output.FirstOrDefault()?.SeriesId}");

    output.Should().HaveCount(2);
    output[0].SeriesId.Should().Be(11);
    output[0].LastChapterTitle.Should().Be("Chương 1");
  }

  [Fact]
  public async Task GetUserHistoryAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    var service = new HistoryService(db);
    var output = await service.GetUserHistoryAsync(-1);

    _output.WriteLine("Input: userId=-1");
    _output.WriteLine($"Output: count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetUserHistoryAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new HistoryService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetUserHistoryAsync(1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task RemoveFromHistoryAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    db.ReadingHistories.Add(new ReadingHistory
    {
      HistoryId = 110,
      UserId = 1,
      SeriesId = 10,
      LastChapterId = 1001,
      LastPageNumber = 10,
      LastReadAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new HistoryService(db);
    var output = await service.RemoveFromHistoryAsync(1, 10);

    _output.WriteLine("Input: userId=1,seriesId=10");
    _output.WriteLine($"Output: removed={output}");

    output.Should().BeTrue();
    (await db.ReadingHistories.AnyAsync(h => h.UserId == 1 && h.SeriesId == 10)).Should().BeFalse();
  }

  [Fact]
  public async Task RemoveFromHistoryAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    var service = new HistoryService(db);
    var output = await service.RemoveFromHistoryAsync(1, 10);

    _output.WriteLine("Input: remove non-existing history");
    _output.WriteLine($"Output: removed={output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task RemoveFromHistoryAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new HistoryService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.RemoveFromHistoryAsync(1, 10));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task ClearAllHistoryAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    db.ReadingHistories.AddRange(
      new ReadingHistory
      {
        HistoryId = 120,
        UserId = 1,
        SeriesId = 10,
        LastChapterId = 1000,
        LastPageNumber = 1,
        LastReadAt = DateTime.UtcNow.AddDays(-2)
      },
      new ReadingHistory
      {
        HistoryId = 121,
        UserId = 1,
        SeriesId = 11,
        LastChapterId = 1100,
        LastPageNumber = 9,
        LastReadAt = DateTime.UtcNow.AddDays(-1)
      },
      new ReadingHistory
      {
        HistoryId = 122,
        UserId = 2,
        SeriesId = 11,
        LastChapterId = 1100,
        LastPageNumber = 3,
        LastReadAt = DateTime.UtcNow
      });
    await db.SaveChangesAsync();

    var service = new HistoryService(db);
    var output = await service.ClearAllHistoryAsync(1);

    _output.WriteLine("Input: clear userId=1 histories");
    _output.WriteLine($"Output: cleared={output}");

    output.Should().BeTrue();
    (await db.ReadingHistories.CountAsync(h => h.UserId == 1)).Should().Be(0);
    (await db.ReadingHistories.CountAsync(h => h.UserId == 2)).Should().Be(1);
  }

  [Fact]
  public async Task ClearAllHistoryAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    var service = new HistoryService(db);
    var output = await service.ClearAllHistoryAsync(1);

    _output.WriteLine("Input: clear userId=1 with no history rows");
    _output.WriteLine($"Output: cleared={output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task ClearAllHistoryAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new HistoryService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.ClearAllHistoryAsync(1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetReadingStatsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    db.ReadingHistories.AddRange(
      new ReadingHistory
      {
        HistoryId = 130,
        UserId = 1,
        SeriesId = 10,
        LastChapterId = 1001,
        LastPageNumber = 10,
        LastReadAt = DateTime.UtcNow.AddDays(-1)
      },
      new ReadingHistory
      {
        HistoryId = 131,
        UserId = 1,
        SeriesId = 11,
        LastChapterId = 1100,
        LastPageNumber = 2,
        LastReadAt = DateTime.UtcNow
      });
    await db.SaveChangesAsync();

    var service = new HistoryService(db);
    var output = await service.GetReadingStatsAsync(1);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: totalSeries={output.TotalSeriesRead}, totalFollowing={output.TotalFollowing}, totalRated={output.TotalRated}, totalBookmarks={output.TotalBookmarks}");

    output.TotalSeriesRead.Should().Be(2);
    output.TotalChaptersRead.Should().Be(2);
    output.TotalFollowing.Should().Be(1);
    output.TotalRated.Should().Be(1);
    output.TotalBookmarks.Should().Be(1);
    output.LastActiveAt.Should().NotBeNull();
    output.TopGenres.Should().NotBeEmpty();
    output.MonthlyActivity.Should().HaveCount(6);
    output.RecentActivity.Should().HaveCount(2);
  }

  [Fact]
  public async Task GetReadingStatsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedHistoryBaseAsync(db);

    var service = new HistoryService(db);
    var output = await service.GetReadingStatsAsync(-1);

    _output.WriteLine("Input: userId=-1");
    _output.WriteLine($"Output: totalSeries={output.TotalSeriesRead}, monthlyCount={output.MonthlyActivity.Count}");

    output.TotalSeriesRead.Should().Be(0);
    output.TotalFollowing.Should().Be(0);
    output.MonthlyActivity.Should().HaveCount(6);
  }

  [Fact]
  public async Task GetReadingStatsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new HistoryService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetReadingStatsAsync(1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
