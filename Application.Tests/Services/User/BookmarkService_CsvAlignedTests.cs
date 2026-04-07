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

public class BookmarkService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public BookmarkService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedBookmarkBaseAsync(MlndexDbContext db)
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

    db.Series.Add(new Series
    {
      SeriesId = 10,
      CreatorId = 200,
      Title = "Series A",
      CoverImageUrl = "https://img/series-a.jpg",
      Status = SeriesStatus.ONGOING,
      ModerationStatus = ModerationStatus.APPROVED,
      CreatedAt = DateTime.UtcNow
    });

    db.Chapters.AddRange(
      new Chapter
      {
        ChapterId = 1000,
        SeriesId = 10,
        ChapterNumber = 1,
        Title = "Chapter 1",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-2)
      },
      new Chapter
      {
        ChapterId = 1001,
        SeriesId = 10,
        ChapterNumber = 2,
        Title = "Chapter 2",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-1)
      });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task UpsertBookmarkAsync_TC01_Success_Create()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);
    var service = new BookmarkService(db);

    var input = new BookmarkRequestDto
    {
      SeriesId = 10,
      ChapterId = 1000,
      Note = "Bắt đầu đọc"
    };

    var output = await service.UpsertBookmarkAsync(1, input);

    _output.WriteLine($"Input: userId=1, seriesId={input.SeriesId}, chapterId={input.ChapterId}, note={input.Note}");
    _output.WriteLine($"Output: bookmarkId={output.BookmarkId}, chapterTitle={output.ChapterTitle}, note={output.Note}");

    output.BookmarkId.Should().BeGreaterThan(0);
    output.SeriesId.Should().Be(10);
    output.ChapterId.Should().Be(1000);
    output.ChapterTitle.Should().Be("Chương 1");
    output.Note.Should().Be("Bắt đầu đọc");

    var count = await db.Bookmarks.CountAsync();
    count.Should().Be(1);
  }

  [Fact]
  public async Task UpsertBookmarkAsync_TC02_BusinessRule_UpdateExisting()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);

    db.Bookmarks.Add(new Bookmark
    {
      BookmarkId = 500,
      UserId = 1,
      SeriesId = 10,
      ChapterId = 1000,
      Note = "Note cũ",
      BookmarkedAt = DateTime.UtcNow.AddDays(-5)
    });
    await db.SaveChangesAsync();

    var service = new BookmarkService(db);

    var output = await service.UpsertBookmarkAsync(1, new BookmarkRequestDto
    {
      SeriesId = 10,
      ChapterId = 1001,
      Note = "Note mới"
    });

    _output.WriteLine("Input: upsert same user+series to update chapter/note");
    _output.WriteLine($"Output: bookmarkId={output.BookmarkId}, chapterId={output.ChapterId}, note={output.Note}");

    output.BookmarkId.Should().Be(500);
    output.ChapterId.Should().Be(1001);
    output.ChapterTitle.Should().Be("Chương 2");
    output.Note.Should().Be("Note mới");

    var count = await db.Bookmarks.CountAsync();
    count.Should().Be(1);
  }

  [Fact]
  public async Task UpsertBookmarkAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new BookmarkService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UpsertBookmarkAsync(1, new BookmarkRequestDto
    {
      SeriesId = 10,
      ChapterId = 1000,
      Note = "x"
    }));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetUserBookmarksAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);

    db.Bookmarks.AddRange(
      new Bookmark
      {
        BookmarkId = 600,
        UserId = 1,
        SeriesId = 10,
        ChapterId = 1000,
        Note = "c1",
        BookmarkedAt = DateTime.UtcNow.AddMinutes(-10)
      },
      new Bookmark
      {
        BookmarkId = 601,
        UserId = 1,
        SeriesId = 10,
        ChapterId = 1001,
        Note = "c2",
        BookmarkedAt = DateTime.UtcNow
      });
    await db.SaveChangesAsync();

    var service = new BookmarkService(db);
    var output = await service.GetUserBookmarksAsync(1);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: count={output.Count}, firstBookmarkId={output.FirstOrDefault()?.BookmarkId}");

    output.Should().HaveCount(2);
    output[0].BookmarkId.Should().Be(601);
    output[1].BookmarkId.Should().Be(600);
  }

  [Fact]
  public async Task GetUserBookmarksAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);

    var service = new BookmarkService(db);
    var output = await service.GetUserBookmarksAsync(-1);

    _output.WriteLine("Input: userId=-1");
    _output.WriteLine($"Output: count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetUserBookmarksAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new BookmarkService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetUserBookmarksAsync(1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetBookmarkForSeriesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);

    db.Bookmarks.Add(new Bookmark
    {
      BookmarkId = 700,
      UserId = 1,
      SeriesId = 10,
      ChapterId = 1001,
      Note = "đang theo dõi",
      BookmarkedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new BookmarkService(db);
    var output = await service.GetBookmarkForSeriesAsync(1, 10);

    _output.WriteLine("Input: userId=1, seriesId=10");
    _output.WriteLine($"Output: bookmarkId={output?.BookmarkId}, chapterTitle={output?.ChapterTitle}");

    output.Should().NotBeNull();
    output!.BookmarkId.Should().Be(700);
    output.ChapterTitle.Should().Be("Chương 2");
  }

  [Fact]
  public async Task GetBookmarkForSeriesAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);
    var service = new BookmarkService(db);

    var output = await service.GetBookmarkForSeriesAsync(1, 10);

    _output.WriteLine("Input: userId=1, seriesId=10 without bookmark");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetBookmarkForSeriesAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new BookmarkService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetBookmarkForSeriesAsync(1, 10));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task DeleteBookmarkAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);

    db.Bookmarks.Add(new Bookmark
    {
      BookmarkId = 800,
      UserId = 1,
      SeriesId = 10,
      ChapterId = 1000,
      Note = "will delete",
      BookmarkedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new BookmarkService(db);
    var output = await service.DeleteBookmarkAsync(1, 800);

    _output.WriteLine("Input: userId=1, bookmarkId=800");
    _output.WriteLine($"Output: deleted={output}");

    output.Should().BeTrue();
    (await db.Bookmarks.AnyAsync(b => b.BookmarkId == 800)).Should().BeFalse();
  }

  [Fact]
  public async Task DeleteBookmarkAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBookmarkBaseAsync(db);

    var service = new BookmarkService(db);
    var output = await service.DeleteBookmarkAsync(1, 9999);

    _output.WriteLine("Input: userId=1, bookmarkId=9999");
    _output.WriteLine($"Output: deleted={output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task DeleteBookmarkAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new BookmarkService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.DeleteBookmarkAsync(1, 1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
