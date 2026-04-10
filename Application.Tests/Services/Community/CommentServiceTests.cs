using System;
using System.Threading.Tasks;
using Application.DTOs.Common;
using Application.DTOs.Community;
using Application.Exceptions;
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
  public class CommentServiceTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;

    public CommentServiceTests(DatabaseFixture fixture)
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
    public async Task CreateAsync_Should_Create_TopLevel_Comment()
    {
      var user = await CreateUserAsync(101, "comment_user1");
      using var db = _fixture.CreateDbContext();
      var service = new CommentService(db);

      var request = new CreateCommentRequest
      {
        Content = "Hello World",
        TargetId = 1,
        TargetType = CommentTargetType.CHAPTER,
        ParentCommentId = null
      };

      var response = await service.CreateAsync(user.UserId, request);

      response.Should().NotBeNull();
      response.Content.Should().Be("Hello World");
      response.UserId.Should().Be(user.UserId);

      var saved = await db.Comments.FirstOrDefaultAsync(c => c.CommentId == response.CommentId);
      saved.Should().NotBeNull();
      saved!.TargetId.Should().Be(1);
      saved.TargetType.Should().Be(CommentTargetType.CHAPTER);
      saved.ParentCommentId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_UserNotFound()
    {
      using var db = _fixture.CreateDbContext();
      var service = new CommentService(db);

      var request = new CreateCommentRequest
      {
        Content = "Hello World",
        TargetId = 1,
        TargetType = CommentTargetType.CHAPTER
      };

      var act = async () => await service.CreateAsync(999, request);

      await act.Should().ThrowAsync<AppException>().Where(e => e.ErrorCode == ErrorCodes.USER_NOT_FOUND);
    }

    [Fact]
    public async Task CreateAsync_Should_Create_Reply()
    {
      var user1 = await CreateUserAsync(102, "comment_user2");
      var user2 = await CreateUserAsync(103, "comment_user3");

      using var db = _fixture.CreateDbContext();
      var parentComment = new Comment
      {
        UserId = user1.UserId,
        TargetId = 1,
        TargetType = CommentTargetType.SERIES,
        Content = "Parent",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };
      db.Comments.Add(parentComment);
      await db.SaveChangesAsync();

      var service = new CommentService(db);
      var request = new CreateCommentRequest
      {
        Content = "Reply",
        TargetId = 999, // Intentional mismatch to verify it's overwritten
        TargetType = CommentTargetType.CHAPTER, // Intentional mismatch
        ParentCommentId = parentComment.CommentId
      };

      var response = await service.CreateAsync(user2.UserId, request);

      var saved = await db.Comments.FirstOrDefaultAsync(c => c.CommentId == response.CommentId);
      saved.Should().NotBeNull();
      saved!.ParentCommentId.Should().Be(parentComment.CommentId);
      saved.TargetId.Should().Be(parentComment.TargetId);
      saved.TargetType.Should().Be(parentComment.TargetType);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_ParentNotFound()
    {
      var user = await CreateUserAsync(104, "comment_user4");
      using var db = _fixture.CreateDbContext();
      var service = new CommentService(db);

      var request = new CreateCommentRequest
      {
        Content = "Reply",
        TargetId = 1,
        TargetType = CommentTargetType.CHAPTER,
        ParentCommentId = 9999
      };

      var act = async () => await service.CreateAsync(user.UserId, request);
      await act.Should().ThrowAsync<AppException>().Where(e => e.ErrorCode == ErrorCodes.COMMENT_NOT_FOUND);
    }

    [Fact]
    public async Task CreateAsync_Should_Throw_When_MaxDepthReached()
    {
      var user = await CreateUserAsync(105, "comment_user5");

      using var db = _fixture.CreateDbContext();
      var parent = new Comment
      {
        UserId = user.UserId,
        TargetId = 1,
        TargetType = CommentTargetType.SERIES,
        Content = "Parent",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };
      db.Comments.Add(parent);
      await db.SaveChangesAsync();

      var child = new Comment
      {
        UserId = user.UserId,
        TargetId = 1,
        TargetType = CommentTargetType.SERIES,
        Content = "Child",
        ParentCommentId = parent.CommentId,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };
      db.Comments.Add(child);
      await db.SaveChangesAsync();

      var service = new CommentService(db);
      var request = new CreateCommentRequest
      {
        Content = "Grandchild",
        TargetId = 1,
        TargetType = CommentTargetType.SERIES,
        ParentCommentId = child.CommentId
      };

      var act = async () => await service.CreateAsync(user.UserId, request);
      await act.Should().ThrowAsync<AppException>().Where(e => e.ErrorCode == ErrorCodes.COMMENT_MAX_DEPTH_REACHED);
    }

    [Fact]
    public async Task DeleteAsync_Should_SoftDelete()
    {
      var user = await CreateUserAsync(106, "comment_user6");

      using var db = _fixture.CreateDbContext();
      var comment = new Comment
      {
        UserId = user.UserId,
        TargetId = 1,
        TargetType = CommentTargetType.SERIES,
        Content = "Bad stuff",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };
      db.Comments.Add(comment);
      await db.SaveChangesAsync();

      var service = new CommentService(db);
      await service.DeleteAsync(comment.CommentId, user.UserId);

      var saved = await db.Comments.FirstOrDefaultAsync(c => c.CommentId == comment.CommentId);
      saved.Should().NotBeNull();
      saved!.IsDeleted.Should().BeTrue();
      saved.Content.Should().Be("[deleted]");
    }

    [Fact]
    public async Task DeleteAsync_Should_Throw_When_NotOwner()
    {
      var owner = await CreateUserAsync(107, "comment_owner");
      var hacker = await CreateUserAsync(108, "comment_hacker");

      using var db = _fixture.CreateDbContext();
      var comment = new Comment
      {
        UserId = owner.UserId,
        TargetId = 1,
        TargetType = CommentTargetType.SERIES,
        Content = "Good stuff",
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
      };
      db.Comments.Add(comment);
      await db.SaveChangesAsync();

      var service = new CommentService(db);
      var act = async () => await service.DeleteAsync(comment.CommentId, hacker.UserId);
      await act.Should().ThrowAsync<AppException>().Where(e => e.ErrorCode == ErrorCodes.FORBIDDEN);
    }
  }
}
