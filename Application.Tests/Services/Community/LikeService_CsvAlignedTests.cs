using System;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Community;
using Application.Services.Community;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.CommunityServices;

public class LikeService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public LikeService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedUsersAsync(MlndexDbContext db)
  {
    db.Users.AddRange(
      new User
      {
        UserId = 1,
        Username = "u1",
        Email = "u1@test.com",
        PasswordHash = "hash",
        DisplayName = "User One",
        IsEmailVerified = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
      },
      new User
      {
        UserId = 2,
        Username = "u2",
        Email = "u2@test.com",
        PasswordHash = "hash",
        DisplayName = "User Two",
        IsEmailVerified = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
      });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task ToggleAsync_TC01_Success_LikeNew()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUsersAsync(db);

    var service = new LikeService(db);
    var input = new LikeRequest { TargetId = 100, TargetType = LikeTargetType.SERIES };

    var output = await service.ToggleAsync(1, input);

    _output.WriteLine($"Input: userId=1,targetId={input.TargetId},targetType={input.TargetType}");
    _output.WriteLine($"Output: liked={output.Liked},total={output.TotalLikes}");

    output.Liked.Should().BeTrue();
    output.TotalLikes.Should().Be(1);

    (await db.Likes.CountAsync()).Should().Be(1);
  }

  [Fact]
  public async Task ToggleAsync_TC02_BusinessRule_UnlikeExisting()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUsersAsync(db);

    db.Likes.Add(new Like
    {
      LikeId = 10,
      UserId = 1,
      TargetId = 100,
      TargetType = LikeTargetType.SERIES,
      CreatedAt = DateTime.UtcNow.AddMinutes(-1)
    });
    await db.SaveChangesAsync();

    var service = new LikeService(db);
    var output = await service.ToggleAsync(1, new LikeRequest { TargetId = 100, TargetType = LikeTargetType.SERIES });

    _output.WriteLine("Input: toggle existing like userId=1,target=100,SERIES");
    _output.WriteLine($"Output: liked={output.Liked},total={output.TotalLikes}");

    output.Liked.Should().BeFalse();
    output.TotalLikes.Should().Be(0);
    (await db.Likes.CountAsync()).Should().Be(0);
  }

  [Fact]
  public async Task ToggleAsync_TC03_Success_KeepOtherUsersLikes()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUsersAsync(db);

    db.Likes.Add(new Like
    {
      LikeId = 11,
      UserId = 2,
      TargetId = 100,
      TargetType = LikeTargetType.SERIES,
      CreatedAt = DateTime.UtcNow.AddMinutes(-2)
    });
    await db.SaveChangesAsync();

    var service = new LikeService(db);
    var output = await service.ToggleAsync(1, new LikeRequest { TargetId = 100, TargetType = LikeTargetType.SERIES });

    _output.WriteLine("Input: user1 toggles like while user2 already liked same target");
    _output.WriteLine($"Output: liked={output.Liked},total={output.TotalLikes}");

    output.Liked.Should().BeTrue();
    output.TotalLikes.Should().Be(2);
  }
}
