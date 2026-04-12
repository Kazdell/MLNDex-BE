using System;
using System.Threading.Tasks;
using Application.DTOs.Community;
using Application.Services.Community;
using Application.Tests.Shared;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Application.Tests.Services.Community
{
  [Collection("Database collection")]
  public class LikeServiceTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;

    public LikeServiceTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
      await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<User> CreateUserAsync(int id, string username)
    {
      using var db = _fixture.CreateDbContext();
      var user = new User
      {
        UserId = id,
        Username = username,
        Email = $"{username}@test.com",
        DisplayName = username,
        PasswordHash = "X"
      };
      db.Users.Add(user);
      await db.SaveChangesAsync();
      return user;
    }

    [Fact]
    public async Task ToggleAsync_Should_AddLike_When_NotExists()
    {
      var user = await CreateUserAsync(201, "like_user1");
      using var db = _fixture.CreateDbContext();
      var service = new LikeService(db);

      var request = new LikeRequest
      {
        TargetId = 5,
        TargetType = LikeTargetType.CHAPTER
      };

      var result = await service.ToggleAsync(user.UserId, request);

      result.Should().NotBeNull();
      result.Liked.Should().BeTrue();
      result.TotalLikes.Should().Be(1);

      var saved = await db.Likes.FirstOrDefaultAsync();
      saved.Should().NotBeNull();
      saved!.UserId.Should().Be(user.UserId);
      saved.TargetId.Should().Be(5);
    }

    [Fact]
    public async Task ToggleAsync_Should_RemoveLike_When_Exists()
    {
      var user = await CreateUserAsync(202, "like_user2");
      using var db = _fixture.CreateDbContext();
      
      var existingLike = new Like
      {
        UserId = user.UserId,
        TargetId = 10,
        TargetType = LikeTargetType.COMMENT,
        CreatedAt = DateTime.UtcNow
      };
      db.Likes.Add(existingLike);
      
      var otherUser = await CreateUserAsync(203, "other_user");
      var otherLike = new Like
      {
        UserId = otherUser.UserId, // Another user
        TargetId = 10,
        TargetType = LikeTargetType.COMMENT,
        CreatedAt = DateTime.UtcNow
      };
      db.Likes.Add(otherLike);
      await db.SaveChangesAsync();

      var service = new LikeService(db);
      var request = new LikeRequest
      {
        TargetId = 10,
        TargetType = LikeTargetType.COMMENT
      };

      var result = await service.ToggleAsync(user.UserId, request);

      result.Should().NotBeNull();
      result.Liked.Should().BeFalse();
      result.TotalLikes.Should().Be(1); // Only the other like remains

      var saved = await db.Likes.AnyAsync(l => l.UserId == user.UserId && l.TargetId == 10);
      saved.Should().BeFalse();
    }
  }
}
