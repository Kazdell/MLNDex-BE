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

public class FollowService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public FollowService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedFollowBaseAsync(MlndexDbContext db)
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

    db.Series.AddRange(
      new Series
      {
        SeriesId = 10,
        CreatorId = 200,
        Title = "Series A",
        CoverImageUrl = "https://img/a.jpg",
        Status = SeriesStatus.ONGOING,
        ModerationStatus = ModerationStatus.APPROVED,
        AverageRating = 4.5m,
        CreatedAt = DateTime.UtcNow.AddDays(-10)
      },
      new Series
      {
        SeriesId = 11,
        CreatorId = 200,
        Title = "Series B",
        CoverImageUrl = "https://img/b.jpg",
        Status = SeriesStatus.COMPLETED,
        ModerationStatus = ModerationStatus.APPROVED,
        AverageRating = 3.9m,
        CreatedAt = DateTime.UtcNow.AddDays(-8)
      });

    db.Chapters.AddRange(
      new Chapter
      {
        ChapterId = 1000,
        SeriesId = 10,
        ChapterNumber = 1,
        Title = "A-1",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-5)
      },
      new Chapter
      {
        ChapterId = 1001,
        SeriesId = 10,
        ChapterNumber = 2,
        Title = "A-2",
        ContentType = ContentType.TEXT,
        LockStatus = ChapterLockStatus.FREE,
        Status = ChapterStatus.PUBLISHED,
        ModerationStatus = ModerationStatus.APPROVED,
        CreatedAt = DateTime.UtcNow.AddDays(-1)
      });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task FollowAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    var service = new FollowService(db);
    var input = new FollowRequestDto { TargetId = 10, TargetType = "SERIES" };

    var output = await service.FollowAsync(1, input);

    _output.WriteLine($"Input: userId=1, targetId={input.TargetId}, targetType={input.TargetType}");
    _output.WriteLine($"Output: followId={output.FollowId}, targetType={output.TargetType}");

    output.FollowId.Should().BeGreaterThan(0);
    output.TargetId.Should().Be(10);
    output.TargetType.Should().Be("SERIES");
  }

  [Fact]
  public async Task FollowAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    var service = new FollowService(db);

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.FollowAsync(1, new FollowRequestDto
    {
      TargetId = 10,
      TargetType = "BAD_TYPE"
    }));

    _output.WriteLine("Input: targetType=BAD_TYPE");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Invalid TargetType");
  }

  [Fact]
  public async Task FollowAsync_TC03_BusinessRule_AlreadyFollowing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    db.Follows.Add(new Follow
    {
      FollowId = 500,
      UserId = 1,
      TargetId = 10,
      TargetType = FollowTargetType.SERIES,
      FollowedAt = DateTime.UtcNow.AddDays(-1)
    });
    await db.SaveChangesAsync();

    var service = new FollowService(db);

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FollowAsync(1, new FollowRequestDto
    {
      TargetId = 10,
      TargetType = "SERIES"
    }));

    _output.WriteLine("Input: duplicate follow userId=1,targetId=10,SERIES");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Already following this target.");
  }

  [Fact]
  public async Task UnfollowAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    db.Follows.Add(new Follow
    {
      FollowId = 510,
      UserId = 1,
      TargetId = 10,
      TargetType = FollowTargetType.SERIES,
      FollowedAt = DateTime.UtcNow.AddDays(-1)
    });
    await db.SaveChangesAsync();

    var service = new FollowService(db);
    var output = await service.UnfollowAsync(1, 10, "SERIES");

    _output.WriteLine("Input: userId=1,targetId=10,targetType=SERIES");
    _output.WriteLine($"Output: unfollowed={output}");

    output.Should().BeTrue();
    (await db.Follows.AnyAsync(f => f.FollowId == 510)).Should().BeFalse();
  }

  [Fact]
  public async Task UnfollowAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    var service = new FollowService(db);
    var output = await service.UnfollowAsync(1, 10, "SERIES");

    _output.WriteLine("Input: unfollow non-existing relation");
    _output.WriteLine($"Output: unfollowed={output}");

    output.Should().BeFalse();
  }

  [Fact]
  public async Task UnfollowAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new FollowService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UnfollowAsync(1, 10, "SERIES"));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetFollowedSeriesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    db.Follows.AddRange(
      new Follow
      {
        FollowId = 520,
        UserId = 1,
        TargetId = 11,
        TargetType = FollowTargetType.SERIES,
        FollowedAt = DateTime.UtcNow.AddHours(-2)
      },
      new Follow
      {
        FollowId = 521,
        UserId = 1,
        TargetId = 10,
        TargetType = FollowTargetType.SERIES,
        FollowedAt = DateTime.UtcNow.AddHours(-1)
      });
    await db.SaveChangesAsync();

    var service = new FollowService(db);
    var output = await service.GetFollowedSeriesAsync(1);

    _output.WriteLine("Input: userId=1");
    _output.WriteLine($"Output: count={output.Count}, firstSeriesId={output.FirstOrDefault()?.SeriesId}, firstLatestChapter={output.FirstOrDefault()?.LatestChapter}");

    output.Should().HaveCount(2);
    output[0].SeriesId.Should().Be(10);
    output[0].LatestChapter.Should().Be("2");
    output[0].CreatorName.Should().Be("creator_owner");
  }

  [Fact]
  public async Task GetFollowedSeriesAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    var service = new FollowService(db);
    var output = await service.GetFollowedSeriesAsync(-1);

    _output.WriteLine("Input: userId=-1");
    _output.WriteLine($"Output: count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetFollowedSeriesAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new FollowService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetFollowedSeriesAsync(1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task CheckFollowStatusAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    db.Follows.Add(new Follow
    {
      FollowId = 530,
      UserId = 1,
      TargetId = 10,
      TargetType = FollowTargetType.SERIES,
      FollowedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new FollowService(db);
    var output = await service.CheckFollowStatusAsync(1, 10, "SERIES");

    _output.WriteLine("Input: userId=1,targetId=10,targetType=SERIES");
    _output.WriteLine($"Output: isFollowing={output.IsFollowing}, followId={output.FollowId}");

    output.IsFollowing.Should().BeTrue();
    output.FollowId.Should().Be(530);
  }

  [Fact]
  public async Task CheckFollowStatusAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    var service = new FollowService(db);
    var output = await service.CheckFollowStatusAsync(1, 10, "WRONG");

    _output.WriteLine("Input: invalid targetType=WRONG");
    _output.WriteLine($"Output: isFollowing={output.IsFollowing}, followId={output.FollowId}");

    output.IsFollowing.Should().BeFalse();
    output.FollowId.Should().BeNull();
  }

  [Fact]
  public async Task CheckFollowStatusAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new FollowService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.CheckFollowStatusAsync(1, 10, "SERIES"));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetFollowCountAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    db.Follows.AddRange(
      new Follow
      {
        FollowId = 540,
        UserId = 1,
        TargetId = 10,
        TargetType = FollowTargetType.SERIES,
        FollowedAt = DateTime.UtcNow
      },
      new Follow
      {
        FollowId = 541,
        UserId = 2,
        TargetId = 10,
        TargetType = FollowTargetType.SERIES,
        FollowedAt = DateTime.UtcNow
      },
      new Follow
      {
        FollowId = 542,
        UserId = 3,
        TargetId = 10,
        TargetType = FollowTargetType.CREATOR,
        FollowedAt = DateTime.UtcNow
      });
    await db.SaveChangesAsync();

    var service = new FollowService(db);
    var output = await service.GetFollowCountAsync(10, "SERIES");

    _output.WriteLine("Input: targetId=10,targetType=SERIES");
    _output.WriteLine($"Output: count={output}");

    output.Should().Be(2);
  }

  [Fact]
  public async Task GetFollowCountAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedFollowBaseAsync(db);

    var service = new FollowService(db);
    var output = await service.GetFollowCountAsync(10, "WRONG");

    _output.WriteLine("Input: targetId=10,targetType=WRONG");
    _output.WriteLine($"Output: count={output}");

    output.Should().Be(0);
  }

  [Fact]
  public async Task GetFollowCountAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = new FollowService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetFollowCountAsync(10, "SERIES"));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
