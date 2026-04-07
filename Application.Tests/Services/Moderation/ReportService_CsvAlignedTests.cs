using System;
using System.Linq;
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

public class ReportService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public ReportService_CsvAlignedTests(ITestOutputHelper output)
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

  private static async Task SeedAsync(MlndexDbContext db)
  {
    db.Users.Add(new User { UserId = 1, Username = "reporter", Email = "reporter@test.com", DisplayName = "Reporter", PasswordHash = "hash" });
    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 10, UserId = 1, PenName = "Creator" });
    db.Series.Add(new Series { SeriesId = 100, CreatorId = 10, Title = "Series A" });
    db.Chapters.Add(new Chapter { ChapterId = 200, SeriesId = 100, Title = "Chapter A", ChapterNumber = 1, ContentType = ContentType.IMAGE, Status = ChapterStatus.DRAFT, ModerationStatus = ModerationStatus.PENDING, LockStatus = ChapterLockStatus.FREE, LanguageId = 1 });

    db.Reports.Add(new Report
    {
      ReportId = 1,
      ReporterId = 1,
      ContentId = 200,
      ContentType = ReportTargetType.ChapterTranslation,
      Reason = ReportReason.Inappropriate,
      Description = "bad content",
      CreatedAt = DateTime.UtcNow.AddHours(-2)
    });

    db.ModerationQueues.Add(new ModerationQueue
    {
      QueueId = 1,
      ContentId = 200,
      ContentType = ModerationQueueContentType.CHAPTER,
      Priority = QueuePriority.MEDIUM,
      Status = QueueStatus.PENDING,
      FlaggedAt = DateTime.UtcNow.AddHours(-1),
      ReportCount = 1,
      Reports = db.Reports.Local.ToList()
    });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetPendingQueuesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new ReportService(db);

    var output = await service.GetPendingQueuesAsync();

    _output.WriteLine("Input: one pending moderation queue");
    _output.WriteLine($"Output: count={output.Items.Count}, firstQueueId={output.Items.FirstOrDefault()?.QueueId}");

    output.Items.Should().HaveCount(1);
    output.Items[0].QueueId.Should().Be(1);
  }

  [Fact]
  public async Task GetPendingQueuesAsync_TC02_SpecialCase_ReturnsEmptyWhenNoPendingOrInReviewQueue()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var queue = await db.ModerationQueues.FirstAsync(q => q.QueueId == 1);
    queue.Status = QueueStatus.RESOLVED;
    await db.SaveChangesAsync();

    var service = new ReportService(db);
    var output = await service.GetPendingQueuesAsync();

    output.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task GetPendingQueuesAsync_TC03_BusinessRule_IncludesInReviewQueue()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var queue = await db.ModerationQueues.FirstAsync(q => q.QueueId == 1);
    queue.Status = QueueStatus.IN_REVIEW;
    await db.SaveChangesAsync();

    var service = new ReportService(db);
    var output = await service.GetPendingQueuesAsync();

    output.Items.Should().HaveCount(1);
    output.Items[0].Status.Should().Be(QueueStatus.IN_REVIEW);
  }

  [Fact]
  public async Task DecideAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new ReportService(db);

    var output = await service.DecideAsync(1, 99, new ModerationDecisionRequest
    {
      Status = QueueStatus.IN_REVIEW,
      Reason = "taking action"
    });

    _output.WriteLine("Input: queueId=1, status=IN_REVIEW");
    _output.WriteLine($"Output: status={output.Status}, assigned={db.ModerationQueues.First().AssignedTo}");

    output.Status.Should().Be(QueueStatus.IN_REVIEW);
    db.ModerationActions.Should().HaveCount(1);
  }

  [Fact]
  public async Task DecideAsync_TC02_NotFound_WhenQueueMissing()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new ReportService(db);

    var act = () => service.DecideAsync(9999, 99, new ModerationDecisionRequest
    {
      Status = QueueStatus.IN_REVIEW,
      Reason = "missing queue"
    });

    await act.Should().ThrowAsync<KeyNotFoundException>();
  }

  [Fact]
  public async Task DecideAsync_TC03_InvalidInput_WhenQueueAlreadyResolved()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var queue = await db.ModerationQueues.FirstAsync(q => q.QueueId == 1);
    queue.Status = QueueStatus.RESOLVED;
    await db.SaveChangesAsync();

    var service = new ReportService(db);
    var act = () => service.DecideAsync(1, 99, new ModerationDecisionRequest
    {
      Status = QueueStatus.IN_REVIEW,
      Reason = "retry"
    });

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task DecideAsync_TC04_InvalidInput_WhenRequestStatusPending()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new ReportService(db);

    var act = () => service.DecideAsync(1, 99, new ModerationDecisionRequest
    {
      Status = QueueStatus.PENDING,
      Reason = "invalid transition"
    });

    await act.Should().ThrowAsync<InvalidOperationException>();
  }

  [Fact]
  public async Task DecideAsync_TC05_BusinessRule_AssignsModeratorAndSavesActionReason()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedAsync(db);
    var service = new ReportService(db);

    var output = await service.DecideAsync(1, 77, new ModerationDecisionRequest
    {
      Status = QueueStatus.DISMISSED,
      Reason = "insufficient evidence"
    });

    output.Status.Should().Be(QueueStatus.DISMISSED);
    var queue = await db.ModerationQueues.FirstAsync(q => q.QueueId == 1);
    queue.AssignedTo.Should().Be(77);
    queue.AssignedAt.Should().NotBeNull();
    (await db.ModerationActions.FirstAsync()).Reason.Should().Be("insufficient evidence");
  }
}
