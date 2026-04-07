using System;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Creator;
using Application.Interfaces.Notification;
using Application.Services.Creator;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.CreatorServices;

public class ChapterService_RetryModeration_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IStorageService> _mockStorage = new();
  private readonly Mock<INotificationService> _mockNotification = new();
  private readonly Mock<IModerationService> _mockModeration = new();
  private readonly Mock<ILogger<ChapterService>> _mockLogger = new();

  public ChapterService_RetryModeration_CsvAlignedTests(ITestOutputHelper output)
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
  public async Task RetryModerationAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.Chapters.Add(new Chapter
    {
      ChapterId = 123,
      SeriesId = 100,
      ChapterNumber = 1,
      Title = "Retry chapter",
      ContentType = ContentType.IMAGE,
      Status = ChapterStatus.PUBLISHED,
      ModerationStatus = ModerationStatus.REJECTED,
      LockStatus = ChapterLockStatus.FREE,
      LanguageId = 1
    });
    await db.SaveChangesAsync();

    _mockModeration
      .Setup(x => x.EnqueueChapterForModerationAsync(123, It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var service = new ChapterService(db, _mockStorage.Object, _mockLogger.Object, _mockNotification.Object, _mockModeration.Object);

    await service.RetryModerationAsync(123);

    var chapter = await db.Chapters.FirstAsync(c => c.ChapterId == 123);

    _output.WriteLine("Input: rejected chapter id=123");
    _output.WriteLine($"Output: moderation={chapter.ModerationStatus}, status={chapter.Status}");

    chapter.ModerationStatus.Should().Be(ModerationStatus.PENDING);
    chapter.Status.Should().Be(ChapterStatus.DRAFT);
    _mockModeration.Verify(x => x.EnqueueChapterForModerationAsync(123, It.IsAny<CancellationToken>()), Times.Once);
  }

  [Fact]
  public async Task RetryModerationAsync_TC02_NotFound_WhenChapterMissing()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new ChapterService(db, _mockStorage.Object, _mockLogger.Object, _mockNotification.Object, _mockModeration.Object);

    var act = () => service.RetryModerationAsync(9999);

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task RetryModerationAsync_TC03_InvalidInput_WhenActiveQueueAndNoResultYet()
  {
    await using var db = CreateInMemoryDbContext();
    db.Chapters.Add(new Chapter
    {
      ChapterId = 124,
      SeriesId = 100,
      ChapterNumber = 1,
      Title = "Queued chapter",
      ContentType = ContentType.IMAGE,
      Status = ChapterStatus.DRAFT,
      ModerationStatus = ModerationStatus.PENDING,
      LockStatus = ChapterLockStatus.FREE,
      LanguageId = 1
    });
    db.ModerationQueues.Add(new ModerationQueue
    {
      QueueId = 1,
      ContentId = 124,
      ContentType = ModerationQueueContentType.CHAPTER,
      Status = QueueStatus.PENDING,
      FlaggedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = new ChapterService(db, _mockStorage.Object, _mockLogger.Object, _mockNotification.Object, _mockModeration.Object);
    var act = () => service.RetryModerationAsync(124);

    await act.Should().ThrowAsync<InvalidOperationException>();
  }
}
