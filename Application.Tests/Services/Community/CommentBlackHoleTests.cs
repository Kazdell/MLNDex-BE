using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Community;
using Application.Services.Community;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Application.Tests.Shared;

namespace Application.Tests.Services.Community
{
  [Collection("Database collection")]
  public class CommentBlackHoleTests : IAsyncLifetime
  {
    private readonly DatabaseFixture _fixture;

    public CommentBlackHoleTests(DatabaseFixture fixture)
    {
      _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
      await _fixture.ResetDatabaseAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateAsync_WhenReplying_ShouldForceInheritParentTarget()
    {
      // Arrange
      using var db = _fixture.CreateDbContext();
      
      var user1 = new User { Username = "user1", Email = "u1@test.com", DisplayName = "User 1", PasswordHash = "X" };
      var user2 = new User { Username = "user2", Email = "u2@test.com", DisplayName = "User 2", PasswordHash = "X" };
      db.Users.AddRange(user1, user2);
      await db.SaveChangesAsync();
      
      // Real target of parent
      int realTargetId = 100;
      var parentComment = new Comment 
      { 
        UserId = user1.UserId, 
        TargetId = realTargetId, 
        TargetType = CommentTargetType.SERIES, 
        Content = "Original Comment",
        CreatedAt = System.DateTime.UtcNow,
        UpdatedAt = System.DateTime.UtcNow
      };
      db.Comments.Add(parentComment);
      await db.SaveChangesAsync();

      var service = new CommentService(db);

      // Act
      int maliciousTargetId = 999;
      var request = new CreateCommentRequest
      {
        Content = "Reply but I want it elsewhere",
        ParentCommentId = parentComment.CommentId,
        TargetId = maliciousTargetId,             // Trying to hijack! We reply to a Series comment but inject malicious target
        TargetType = CommentTargetType.CHAPTER    // Mix up type as well
      };

      var response = await service.CreateAsync(user2.UserId, request);

      // Assert
      var savedComment = await db.Comments.FirstOrDefaultAsync(c => c.CommentId == response.CommentId);
      
      // Target information MUST be inherited from parent
      savedComment.Should().NotBeNull();
      savedComment!.TargetId.Should().Be(realTargetId);
      savedComment.TargetType.Should().Be(CommentTargetType.SERIES);
      savedComment.TargetId.Should().NotBe(maliciousTargetId);
      savedComment.TargetType.Should().NotBe(CommentTargetType.CHAPTER);
    }
  }
}
