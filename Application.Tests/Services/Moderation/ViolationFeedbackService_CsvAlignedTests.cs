using System;
using System.Threading.Tasks;
using Application.DTOs.Moderation;
using Application.Services.Moderation;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Moderation;

public class ViolationFeedbackService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public ViolationFeedbackService_CsvAlignedTests(ITestOutputHelper output)
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

  [Fact]
  public async Task SendFeedbackAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();

    db.Users.Add(new User { UserId = 1, Username = "owner", Email = "owner@test.com", DisplayName = "Owner", PasswordHash = "hash" });
    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 10, UserId = 1, PenName = "Owner Pen" });
    db.Series.Add(new Series { SeriesId = 100, CreatorId = 10, Title = "Series A" });
    db.Chapters.Add(new Chapter { ChapterId = 200, SeriesId = 100, Title = "Chapter A", ChapterNumber = 1, ContentType = ContentType.IMAGE, Status = ChapterStatus.DRAFT, ModerationStatus = ModerationStatus.REJECTED, LockStatus = ChapterLockStatus.FREE, LanguageId = 1 });
    db.ModerationQueues.Add(new ModerationQueue
    {
      QueueId = 1,
      ContentId = 200,
      ContentType = ModerationQueueContentType.CHAPTER,
      Priority = QueuePriority.MEDIUM,
      Status = QueueStatus.PENDING,
      FlaggedAt = DateTime.UtcNow,
      ReportCount = 1
    });
    await db.SaveChangesAsync();

    var service = new ViolationFeedbackService(db);

    var output = await service.SendFeedbackAsync(1, 99, new ViolationFeedbackRequest
    {
      Message = "Please revise and remove violating content."
    });

    _output.WriteLine("Input: queueId=1, moderatorId=99");
    _output.WriteLine($"Output: queueId={output.QueueId}, message={output.Message}");

    output.QueueId.Should().Be(1);
    db.Notifications.Should().HaveCount(1);
    db.ModerationActions.Should().HaveCount(1);
  }

  [Fact]
  public async Task SendFeedbackAsync_TC02_NotFound_WhenQueueMissing()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new ViolationFeedbackService(db);

    var act = () => service.SendFeedbackAsync(999, 99, new ViolationFeedbackRequest { Message = "x" });

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task SendFeedbackAsync_TC03_InvalidInput_WhenTargetAuthorCannotBeResolved()
  {
    await using var db = CreateInMemoryDbContext();
    db.ModerationQueues.Add(new ModerationQueue
    {
      QueueId = 2,
      ContentId = 9999,
      ContentType = ModerationQueueContentType.CHAPTER,
      Priority = QueuePriority.MEDIUM,
      Status = QueueStatus.PENDING,
      FlaggedAt = DateTime.UtcNow,
      ReportCount = 1
    });
    await db.SaveChangesAsync();

    var service = new ViolationFeedbackService(db);
    var act = () => service.SendFeedbackAsync(2, 99, new ViolationFeedbackRequest { Message = "cannot resolve author" });

    await act.Should().ThrowAsync<InvalidOperationException>();
  }
}
