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

public class CommentService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public CommentService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedBaseAsync(MlndexDbContext db)
  {
    db.Users.AddRange(
      new User
      {
        UserId = 1,
        Username = "u1",
        Email = "u1@test.com",
        DisplayName = "User One",
        PasswordHash = "hash",
        IsEmailVerified = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
      },
      new User
      {
        UserId = 2,
        Username = "u2",
        Email = "u2@test.com",
        DisplayName = "User Two",
        PasswordHash = "hash",
        IsEmailVerified = true,
        IsActive = true,
        CreatedAt = DateTime.UtcNow
      });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task CreateAsync_TC01_Success_RootComment()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new CommentService(db);

    var input = new CreateCommentRequest
    {
      TargetId = 100,
      TargetType = CommentTargetType.SERIES,
      Content = "Great series!"
    };

    var output = await service.CreateAsync(1, input);

    _output.WriteLine($"Input: userId=1,targetId={input.TargetId},targetType={input.TargetType},content={input.Content}");
    _output.WriteLine($"Output: commentId={output.CommentId},username={output.Username},content={output.Content}");

    output.CommentId.Should().BeGreaterThan(0);
    output.UserId.Should().Be(1);
    output.Username.Should().Be("u1");
    output.Content.Should().Be("Great series!");
    output.ParentCommentId.Should().BeNull();
  }

  [Fact]
  public async Task CreateAsync_TC02_BusinessRule_ReplyToParent()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);

    db.Comments.Add(new Comment
    {
      CommentId = 10,
      UserId = 1,
      TargetId = 100,
      TargetType = CommentTargetType.SERIES,
      Content = "Root",
      IsDeleted = false,
      CreatedAt = DateTime.UtcNow.AddMinutes(-10),
      UpdatedAt = DateTime.UtcNow.AddMinutes(-10)
    });
    await db.SaveChangesAsync();

    var service = new CommentService(db);
    var output = await service.CreateAsync(2, new CreateCommentRequest
    {
      TargetId = 100,
      TargetType = CommentTargetType.SERIES,
      Content = "Reply",
      ParentCommentId = 10
    });

    _output.WriteLine("Input: create reply with parentCommentId=10");
    _output.WriteLine($"Output: commentId={output.CommentId},parent={output.ParentCommentId}");

    output.ParentCommentId.Should().Be(10);
  }

  [Fact]
  public async Task CreateAsync_TC03_NotFound_ParentComment()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new CommentService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreateAsync(1, new CreateCommentRequest
    {
      TargetId = 100,
      TargetType = CommentTargetType.SERIES,
      Content = "Reply",
      ParentCommentId = 999
    }));

    _output.WriteLine("Input: parentCommentId=999");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Parent comment");
  }

  [Fact]
  public async Task GetByTargetAsync_TC01_Success_WithReplies()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);

    db.Comments.AddRange(
      new Comment
      {
        CommentId = 20,
        UserId = 1,
        TargetId = 100,
        TargetType = CommentTargetType.SERIES,
        Content = "Root A",
        ParentCommentId = null,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow.AddMinutes(-5),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
      },
      new Comment
      {
        CommentId = 21,
        UserId = 2,
        TargetId = 100,
        TargetType = CommentTargetType.SERIES,
        Content = "Reply A1",
        ParentCommentId = 20,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow.AddMinutes(-4),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-4)
      },
      new Comment
      {
        CommentId = 22,
        UserId = 2,
        TargetId = 100,
        TargetType = CommentTargetType.SERIES,
        Content = "Root B",
        ParentCommentId = null,
        IsDeleted = false,
        CreatedAt = DateTime.UtcNow.AddMinutes(-2),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-2)
      });
    await db.SaveChangesAsync();

    var service = new CommentService(db);
    var output = await service.GetByTargetAsync(100, CommentTargetType.SERIES, page: 1, pageSize: 10);

    _output.WriteLine("Input: targetId=100,targetType=SERIES,page=1,pageSize=10");
    _output.WriteLine($"Output: total={output.TotalCount},roots={output.Items.Count},firstRoot={output.Items.FirstOrDefault()?.CommentId}");

    output.TotalCount.Should().Be(2);
    output.Items.Should().HaveCount(2);
    output.Items[0].CommentId.Should().Be(22);
    output.Items[1].Replies.Should().HaveCount(1);
    output.Items[1].Replies[0].CommentId.Should().Be(21);
  }

  [Fact]
  public async Task GetByTargetAsync_TC02_Empty()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new CommentService(db);

    var output = await service.GetByTargetAsync(999, CommentTargetType.SERIES, page: 1, pageSize: 10);

    _output.WriteLine("Input: targetId=999");
    _output.WriteLine($"Output: total={output.TotalCount},roots={output.Items.Count}");

    output.TotalCount.Should().Be(0);
    output.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task GetByTargetAsync_TC03_BusinessRule_ExcludesDeletedRootComments()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);

    db.Comments.Add(new Comment
    {
      CommentId = 23,
      UserId = 1,
      TargetId = 100,
      TargetType = CommentTargetType.SERIES,
      Content = "Deleted root",
      ParentCommentId = null,
      IsDeleted = true,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new CommentService(db);
    var output = await service.GetByTargetAsync(100, CommentTargetType.SERIES, page: 1, pageSize: 10);

    output.TotalCount.Should().Be(1);
    output.Items.Should().HaveCount(1);
    output.Items[0].IsDeleted.Should().BeTrue();
    output.Items[0].Content.Should().Be("[deleted]");
  }

  [Fact]
  public async Task DeleteAsync_TC01_Success_SoftDelete()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);

    db.Comments.Add(new Comment
    {
      CommentId = 30,
      UserId = 1,
      TargetId = 100,
      TargetType = CommentTargetType.SERIES,
      Content = "Will delete",
      IsDeleted = false,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new CommentService(db);
    await service.DeleteAsync(30, 1);

    var deleted = await db.Comments.FirstAsync(c => c.CommentId == 30);

    _output.WriteLine("Input: commentId=30,userId=1");
    _output.WriteLine($"Output: isDeleted={deleted.IsDeleted},content={deleted.Content}");

    deleted.IsDeleted.Should().BeTrue();
    deleted.Content.Should().Be("[deleted]");
  }

  [Fact]
  public async Task DeleteAsync_TC02_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);
    var service = new CommentService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeleteAsync(999, 1));

    _output.WriteLine("Input: commentId=999,userId=1");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Comment không tồn tại");
  }

  [Fact]
  public async Task DeleteAsync_TC03_Unauthorized()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedBaseAsync(db);

    db.Comments.Add(new Comment
    {
      CommentId = 31,
      UserId = 1,
      TargetId = 100,
      TargetType = CommentTargetType.SERIES,
      Content = "Owner only",
      IsDeleted = false,
      CreatedAt = DateTime.UtcNow,
      UpdatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new CommentService(db);

    var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(31, 2));

    _output.WriteLine("Input: delete commentId=31 by userId=2");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Không thể xóa comment của người khác");
  }
}
