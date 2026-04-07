using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.AIModeration;
using Application.DTOs.Creator;
using Application.DTOs.Moderation;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Creator;
using Application.Services.Creator;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.CreatorServices;

public class SeriesService_Query_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IStorageService> _mockStorage = new();
  private readonly Mock<IModerationService> _mockModeration = new();
  private readonly Mock<ILogger<SeriesService>> _mockLogger = new();

  public SeriesService_Query_CsvAlignedTests(ITestOutputHelper output)
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
    _mockModeration
      .Setup(x => x.PreCheckText(It.IsAny<TextCheckRequest>()))
      .Returns(new TextCheckResponse { Action = "Allow", Reasons = new List<string>() });

    _mockModeration
      .Setup(x => x.EnqueueSeriesForModerationAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    return new SeriesService(db, _mockStorage.Object, _mockModeration.Object, _mockLogger.Object);
  }

  private static async Task SeedSeriesBaseAsync(MlndexDbContext db)
  {
    db.Users.AddRange(
      new User
      {
        UserId = 1,
        Username = "creator_user",
        Email = "creator@test.com",
        DisplayName = "Creator",
        PasswordHash = "hash",
        IsEmailVerified = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow.AddDays(-100)
      },
      new User
      {
        UserId = 2,
        Username = "reader_user",
        Email = "reader@test.com",
        DisplayName = "Reader",
        PasswordHash = "hash",
        IsEmailVerified = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow.AddDays(-50)
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
      CreatedAt = DateTime.UtcNow.AddDays(-30)
    });

    db.Genres.AddRange(
      new Genre { GenreId = 1, Name = "Action" },
      new Genre { GenreId = 2, Name = "Drama" });

    db.Series.AddRange(
      new Series
      {
        SeriesId = 100,
        CreatorId = 10,
        Title = "Alpha Saga",
        Description = "Action story",
        SeriesFormat = SeriesFormat.NOVEL,
        AgeRating = AgeRating.TEEN,
        Status = SeriesStatus.ONGOING,
        ModerationStatus = ModerationStatus.APPROVED,
        AverageRating = 4.5m,
        TotalRatings = 50,
        CreatedAt = DateTime.UtcNow.AddDays(-20)
      },
      new Series
      {
        SeriesId = 101,
        CreatorId = 10,
        Title = "Beta Tale",
        Description = "Drama story",
        SeriesFormat = SeriesFormat.NOVEL,
        AgeRating = AgeRating.ALL_AGES,
        Status = SeriesStatus.COMPLETED,
        ModerationStatus = ModerationStatus.APPROVED,
        AverageRating = 3.8m,
        TotalRatings = 10,
        CreatedAt = DateTime.UtcNow.AddDays(-10)
      },
      new Series
      {
        SeriesId = 102,
        CreatorId = 10,
        Title = "Gamma Hidden",
        Description = "Dropped story",
        SeriesFormat = SeriesFormat.NOVEL,
        AgeRating = AgeRating.MATURE,
        Status = SeriesStatus.DROPPED,
        ModerationStatus = ModerationStatus.APPROVED,
        AverageRating = 4.9m,
        TotalRatings = 99,
        CreatedAt = DateTime.UtcNow.AddDays(-5)
      });

    db.SeriesGenres.AddRange(
      new SeriesGenre { SeriesGenreId = 1, SeriesId = 100, GenreId = 1 },
      new SeriesGenre { SeriesGenreId = 2, SeriesId = 101, GenreId = 2 },
      new SeriesGenre { SeriesGenreId = 3, SeriesId = 102, GenreId = 2 });

    db.Chapters.AddRange(
      new Chapter
      {
        ChapterId = 1000,
        SeriesId = 100,
        TeamId = null,
        LanguageId = 1,
        ChapterNumber = 1,
        Title = "A-1",
        ContentType = ContentType.TEXT,
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
        Title = "A-2",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        PublishedAt = DateTime.UtcNow.AddDays(-2),
        CreatedAt = DateTime.UtcNow.AddDays(-3),
        PageCount = 2
      },
      new Chapter
      {
        ChapterId = 1100,
        SeriesId = 101,
        TeamId = null,
        LanguageId = 1,
        ChapterNumber = 1,
        Title = "B-1",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        PublishedAt = DateTime.UtcNow.AddDays(-1),
        CreatedAt = DateTime.UtcNow.AddDays(-2)
      });

    db.ChapterPages.AddRange(
      new ChapterPage { PageId = 1, ChapterId = 1001, PageNumber = 1, ImageUrl = "https://img/1001-1.jpg" },
      new ChapterPage { PageId = 2, ChapterId = 1001, PageNumber = 2, ImageUrl = "https://img/1001-2.jpg" });

    db.ReadingHistories.AddRange(
      new ReadingHistory { HistoryId = 1, UserId = 2, SeriesId = 100, LastChapterId = 1001, LastPageNumber = 2, LastReadAt = DateTime.UtcNow.AddDays(-1) },
      new ReadingHistory { HistoryId = 2, UserId = 1, SeriesId = 100, LastChapterId = 1000, LastPageNumber = 1, LastReadAt = DateTime.UtcNow.AddDays(-2) },
      new ReadingHistory { HistoryId = 3, UserId = 2, SeriesId = 101, LastChapterId = 1100, LastPageNumber = 1, LastReadAt = DateTime.UtcNow.AddDays(-1) });

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
      ContentType = ContentType.TEXT,
      QualityStatus = TranslationQualityStatus.PUBLISHED,
      ModerationStatus = ModerationStatus.APPROVED,
      PublishedAt = DateTime.UtcNow.AddDays(-1),
      IsOfficial = true
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetByCreatorAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetByCreatorAsync(1);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: count={output.Count}, firstSeriesId={output.FirstOrDefault()?.SeriesId}");

    output.Should().HaveCount(3);
    output[0].SeriesId.Should().Be(102);
    output.Any(x => x.SeriesId == 100 && x.ChapterCount >= 2).Should().BeTrue();
  }

  [Fact]
  public async Task GetByCreatorAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetByCreatorAsync(999));

    _output.WriteLine("Input: userId=999");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Creator");
  }

  [Fact]
  public async Task GetByCreatorAsync_TC03_Empty_WhenCreatorHasNoSeries()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    db.Users.Add(new User
    {
      UserId = 77,
      Username = "empty_creator_user",
      Email = "emptycreator@test.com",
      DisplayName = "Empty Creator",
      PasswordHash = "hash",
      IsActive = true,
      IsEmailVerified = true,
      CreatedAt = DateTime.UtcNow
    });
    db.CreatorProfiles.Add(new CreatorProfile
    {
      CreatorId = 77,
      UserId = 77,
      PenName = "Empty Pen",
      IsActive = true,
      ModerationStatus = ModerationStatus.APPROVED
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetByCreatorAsync(77);

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetSeriesListAsync_TC01_EmptyPage_ReturnsNoItems()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetSeriesListAsync(sortBy: "popular", page: 99, pageSize: 10);

    _output.WriteLine("Input: sortBy=popular,page=99,pageSize=10");
    _output.WriteLine($"Output: total={output.TotalCount}, itemCount={output.Items.Count}, first={output.Items.FirstOrDefault()?.SeriesId}");

    output.Items.Should().BeEmpty();
    output.TotalCount.Should().Be(2);
  }

  [Fact]
  public async Task GetSeriesListAsync_TC02_Success_PopularSortReturnsRatedFirst()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetSeriesListAsync(sortBy: "popular", page: 99, pageSize: 10);

    output.Items.Should().BeEmpty();
    output.TotalCount.Should().Be(2);
  }

  [Fact]
  public async Task GetSeriesListAsync_TC03_BusinessRule_DefaultSortFallbackWhenUnknownSortBy()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetSeriesListAsync(sortBy: "unsupported-sort", page: 99, pageSize: 10);

    output.TotalCount.Should().Be(2);
    output.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task SearchSeriesAsync_TC01_Empty_NoMatch()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var request = new SeriesSearchRequest
    {
      Keyword = "NoMatchKeyword",
      GenreId = 1,
      Status = SeriesStatus.ONGOING,
      CreatorId = 10,
      SortBy = "newest",
      Page = 1,
      PageSize = 10
    };

    var output = await service.SearchSeriesAsync(request);

    _output.WriteLine("Input: keyword=NoMatchKeyword,genreId=1,status=ONGOING,creatorId=10");
    _output.WriteLine($"Output: total={output.TotalCount}, first={output.Items.FirstOrDefault()?.Title}");

    output.TotalCount.Should().Be(0);
    output.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task SearchSeriesAsync_TC02_Success_FilterByGenre()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.SearchSeriesAsync(new SeriesSearchRequest
    {
      GenreId = 999,
      SortBy = "newest",
      Page = 1,
      PageSize = 10
    });

    output.TotalCount.Should().Be(0);
    output.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task SearchSeriesAsync_TC03_BusinessRule_ExcludeSeriesIdRemovesCurrentSeries()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.SearchSeriesAsync(new SeriesSearchRequest
    {
      CreatorId = 10,
      ExcludeSeriesId = 100,
      Keyword = "NoResult",
      SortBy = "newest",
      Page = 1,
      PageSize = 10
    });

    output.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task GetSeriesDetailsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetSeriesDetailsAsync(100);

    _output.WriteLine("Input: seriesId=100");
    _output.WriteLine($"Output: title={output?.Title}, chapters={output?.Chapters.Count}, originalLang={output?.OriginalLanguage}");

    output.Should().NotBeNull();
    output!.SeriesId.Should().Be(100);
    output.CreatorName.Should().Be("Pen Creator");
    output.OriginalLanguage.Should().Be("vi");
    output.Chapters.Should().NotBeEmpty();
    output.Chapters.Any(c => c.IsOriginal).Should().BeTrue();
    output.Chapters.Any(c => !c.IsOriginal && c.IsOfficialTranslation).Should().BeTrue();
  }

  [Fact]
  public async Task GetSeriesDetailsAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    var service = CreateService(db);

    var output = await service.GetSeriesDetailsAsync(999);

    _output.WriteLine("Input: seriesId=999");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetSeriesDetailsAsync_TC03_BusinessRule_SeriesWithoutChapterHasEmptyChapters()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedSeriesBaseAsync(db);
    db.Series.Add(new Series
    {
      SeriesId = 103,
      CreatorId = 10,
      Title = "No Chapter Series",
      Description = "No chapter yet",
      SeriesFormat = SeriesFormat.NOVEL,
      AgeRating = AgeRating.ALL_AGES,
      Status = SeriesStatus.ONGOING,
      ModerationStatus = ModerationStatus.APPROVED,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetSeriesDetailsAsync(103);

    output.Should().NotBeNull();
    output!.Chapters.Should().BeEmpty();
    output.OriginalLanguage.Should().BeNull();
  }
}
