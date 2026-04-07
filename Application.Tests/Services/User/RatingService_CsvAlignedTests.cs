using System;
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

public class RatingService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public RatingService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedRatingBaseAsync(MlndexDbContext db)
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

    db.Series.Add(new Series
    {
      SeriesId = 10,
      CreatorId = 200,
      Title = "Series A",
      CoverImageUrl = "https://img/a.jpg",
      Status = SeriesStatus.ONGOING,
      ModerationStatus = ModerationStatus.APPROVED,
      CreatedAt = DateTime.UtcNow.AddDays(-30)
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task UpsertRatingAsync_TC01_Success_Create()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    var service = new RatingService(db);
    var input = new RatingRequestDto
    {
      SeriesId = 10,
      Score = 5,
      Review = "Excellent"
    };

    var output = await service.UpsertRatingAsync(1, input);

    _output.WriteLine($"Input: userId=1, seriesId={input.SeriesId}, score={input.Score}");
    _output.WriteLine($"Output: ratingId={output.RatingId}, score={output.Score}, review={output.Review}");

    output.RatingId.Should().BeGreaterThan(0);
    output.Score.Should().Be(5);
    output.Review.Should().Be("Excellent");

    var series = await db.Series.FirstAsync(s => s.SeriesId == 10);
    series.TotalRatings.Should().Be(1);
    series.AverageRating.Should().Be(5m);
  }

  [Fact]
  public async Task UpsertRatingAsync_TC02_BusinessRule_UpdateExisting()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    db.Ratings.Add(new Rating
    {
      RatingId = 500,
      UserId = 1,
      SeriesId = 10,
      Score = 2,
      Review = "Old review",
      CreatedAt = DateTime.UtcNow.AddDays(-2),
      UpdatedAt = DateTime.UtcNow.AddDays(-2)
    });
    await db.SaveChangesAsync();

    var service = new RatingService(db);
    var output = await service.UpsertRatingAsync(1, new RatingRequestDto
    {
      SeriesId = 10,
      Score = 4,
      Review = "Updated review"
    });

    _output.WriteLine("Input: upsert existing rating userId=1,seriesId=10");
    _output.WriteLine($"Output: ratingId={output.RatingId}, score={output.Score}, review={output.Review}");

    output.RatingId.Should().Be(500);
    output.Score.Should().Be(4);
    output.Review.Should().Be("Updated review");

    var count = await db.Ratings.CountAsync();
    count.Should().Be(1);

    var series = await db.Series.FirstAsync(s => s.SeriesId == 10);
    series.TotalRatings.Should().Be(1);
    series.AverageRating.Should().Be(4m);
  }

  [Fact]
  public async Task UpsertRatingAsync_TC03_InvalidInput_ScoreOutOfRange()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    var service = new RatingService(db);

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.UpsertRatingAsync(1, new RatingRequestDto
    {
      SeriesId = 10,
      Score = 6,
      Review = "Invalid"
    }));

    _output.WriteLine("Input: score=6");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("between 1 and 5");
  }

  [Fact]
  public async Task GetUserRatingAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    db.Ratings.Add(new Rating
    {
      RatingId = 600,
      UserId = 1,
      SeriesId = 10,
      Score = 3,
      Review = "Okay",
      CreatedAt = DateTime.UtcNow.AddDays(-3),
      UpdatedAt = DateTime.UtcNow.AddDays(-2)
    });
    await db.SaveChangesAsync();

    var service = new RatingService(db);
    var output = await service.GetUserRatingAsync(1, 10);

    _output.WriteLine("Input: userId=1,seriesId=10");
    _output.WriteLine($"Output: ratingId={output?.RatingId}, score={output?.Score}");

    output.Should().NotBeNull();
    output!.RatingId.Should().Be(600);
    output.Score.Should().Be(3);
  }

  [Fact]
  public async Task GetUserRatingAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    var service = new RatingService(db);
    var output = await service.GetUserRatingAsync(1, 10);

    _output.WriteLine("Input: rating not exists");
    _output.WriteLine($"Output: {(output is null ? "null" : "non-null")}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetUserRatingAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new RatingService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetUserRatingAsync(1, 10));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task DeleteRatingAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    db.Ratings.AddRange(
      new Rating
      {
        RatingId = 700,
        UserId = 1,
        SeriesId = 10,
        Score = 4,
        Review = "Mine",
        CreatedAt = DateTime.UtcNow.AddDays(-2),
        UpdatedAt = DateTime.UtcNow.AddDays(-2)
      },
      new Rating
      {
        RatingId = 701,
        UserId = 2,
        SeriesId = 10,
        Score = 2,
        Review = "Other",
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow.AddDays(-1)
      });
    await db.SaveChangesAsync();

    var service = new RatingService(db);
    var output = await service.DeleteRatingAsync(1, 10);

    _output.WriteLine("Input: delete userId=1 seriesId=10");
    _output.WriteLine($"Output: deleted={output}");

    output.Should().BeTrue();
    (await db.Ratings.AnyAsync(r => r.RatingId == 700)).Should().BeFalse();

    var series = await db.Series.FirstAsync(s => s.SeriesId == 10);
    series.TotalRatings.Should().Be(1);
    series.AverageRating.Should().Be(2m);
  }

  [Fact]
  public async Task DeleteRatingAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    var service = new RatingService(db);
    var output = await service.DeleteRatingAsync(1, 10);

    _output.WriteLine("Input: no rating to delete");
    _output.WriteLine($"Output: deleted={output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task DeleteRatingAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new RatingService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.DeleteRatingAsync(1, 10));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetSeriesRatingSummaryAsync_TC01_Success_WithUserScore()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    db.Ratings.AddRange(
      new Rating
      {
        RatingId = 800,
        UserId = 1,
        SeriesId = 10,
        Score = 5,
        Review = "Great",
        CreatedAt = DateTime.UtcNow.AddDays(-2),
        UpdatedAt = DateTime.UtcNow.AddDays(-2)
      },
      new Rating
      {
        RatingId = 801,
        UserId = 2,
        SeriesId = 10,
        Score = 3,
        Review = "Good",
        CreatedAt = DateTime.UtcNow.AddDays(-1),
        UpdatedAt = DateTime.UtcNow.AddDays(-1)
      });
    await db.SaveChangesAsync();

    var service = new RatingService(db);
    var output = await service.GetSeriesRatingSummaryAsync(10, userId: 1);

    _output.WriteLine("Input: seriesId=10,userId=1");
    _output.WriteLine($"Output: avg={output.AverageRating}, total={output.TotalRatings}, userScore={output.UserScore}");

    output.TotalRatings.Should().Be(2);
    output.AverageRating.Should().Be(4.0m);
    output.UserScore.Should().Be(5);
  }

  [Fact]
  public async Task GetSeriesRatingSummaryAsync_TC02_Empty()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedRatingBaseAsync(db);

    var service = new RatingService(db);
    var output = await service.GetSeriesRatingSummaryAsync(10, userId: 1);

    _output.WriteLine("Input: seriesId=10 has no ratings");
    _output.WriteLine($"Output: avg={output.AverageRating}, total={output.TotalRatings}, userScore={output.UserScore}");

    output.TotalRatings.Should().Be(0);
    output.AverageRating.Should().Be(0m);
    output.UserScore.Should().BeNull();
  }

  [Fact]
  public async Task GetSeriesRatingSummaryAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new RatingService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetSeriesRatingSummaryAsync(10, userId: 1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
