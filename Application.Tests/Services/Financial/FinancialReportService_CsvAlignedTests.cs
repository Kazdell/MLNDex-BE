using System;
using System.Threading.Tasks;
using Application.DTOs.Financial;
using Application.Services.Financial;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Financial;

public class FinancialReportService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public FinancialReportService_CsvAlignedTests(ITestOutputHelper output)
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
  public async Task GetSummaryAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();

    db.Users.Add(new User { UserId = 1, Username = "u1", Email = "u1@test.com", DisplayName = "U1", PasswordHash = "hash" });
    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 10, UserId = 1, PenName = "Creator A" });
    db.Series.Add(new Series { SeriesId = 100, CreatorId = 10, Title = "Series A" });
    db.Chapters.Add(new Chapter { ChapterId = 200, SeriesId = 100, ChapterNumber = 1, Title = "Ch 1", ContentType = ContentType.IMAGE, Status = ChapterStatus.PUBLISHED, ModerationStatus = ModerationStatus.APPROVED, LockStatus = ChapterLockStatus.FREE, LanguageId = 1 });

    db.Transactions.Add(new Transaction
    {
      TransactionId = 1,
      UserId = 1,
      Type = TransactionType.PURCHASE_COIN,
      Status = TransactionStatus.COMPLETED,
      AmountCoins = 1000,
      CreatedAt = DateTime.UtcNow.AddHours(-1)
    });

    db.WithdrawalRequests.Add(new WithdrawalRequest
    {
      WithdrawalId = 1,
      CreatorId = 10,
      AmountCoins = 300,
      AmountVnd = 60000,
      BankAccountInfo = "A-1",
      RequestedAt = DateTime.UtcNow.AddHours(-2),
      ProcessedAt = DateTime.UtcNow.AddHours(-1),
      Status = WithdrawalStatus.COMPLETED
    });

    db.ChapterUnlocks.Add(new ChapterUnlock
    {
      UnlockId = 1,
      UserId = 1,
      ChapterId = 200,
      TransactionId = 1,
      CoinsPaid = 25,
      UnlockSource = UnlockSource.COIN
    });

    await db.SaveChangesAsync();

    var service = new FinancialReportService(db);
    var output = await service.GetSummaryAsync(new FinancialReportRequest
    {
      From = DateTime.UtcNow.AddDays(-1),
      To = DateTime.UtcNow.AddDays(1),
      TopCreators = 5
    });

    _output.WriteLine("Input: one purchase + one completed withdrawal + one unlock");
    _output.WriteLine($"Output: purchased={output.Summary.TotalCoinPurchased}, withdraw={output.Summary.TotalWithdrawCoins}, unlocks={output.Summary.TotalUnlocks}");

    output.Summary.TotalCoinPurchased.Should().Be(1000);
    output.Summary.TotalWithdrawCoins.Should().Be(300);
    output.Summary.TotalUnlocks.Should().Be(1);
    output.TopCreators.Should().ContainSingle();
  }

  [Fact]
  public async Task GetSummaryAsync_TC02_Empty_WhenNoFinancialDataInRange()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new FinancialReportService(db);

    var output = await service.GetSummaryAsync(new FinancialReportRequest
    {
      From = DateTime.UtcNow.AddDays(-1),
      To = DateTime.UtcNow,
      TopCreators = 5
    });

    output.Summary.TotalCoinPurchased.Should().Be(0);
    output.Summary.TotalWithdrawCoins.Should().Be(0);
    output.Summary.TotalUnlocks.Should().Be(0);
    output.TopCreators.Should().BeEmpty();
  }

  [Fact]
  public async Task GetSummaryAsync_TC03_BusinessRule_RespectsTopCreatorsLimit()
  {
    await using var db = CreateInMemoryDbContext();

    db.Users.AddRange(
      new User { UserId = 1, Username = "u1", Email = "u1@test.com", DisplayName = "U1", PasswordHash = "hash" },
      new User { UserId = 2, Username = "u2", Email = "u2@test.com", DisplayName = "U2", PasswordHash = "hash" });
    db.CreatorProfiles.AddRange(
      new CreatorProfile { CreatorId = 10, UserId = 1, PenName = "Creator A" },
      new CreatorProfile { CreatorId = 20, UserId = 2, PenName = "Creator B" });
    db.Series.AddRange(
      new Series { SeriesId = 100, CreatorId = 10, Title = "Series A" },
      new Series { SeriesId = 200, CreatorId = 20, Title = "Series B" });
    db.Chapters.AddRange(
      new Chapter { ChapterId = 1000, SeriesId = 100, ChapterNumber = 1, Title = "A1", ContentType = ContentType.TEXT, Status = ChapterStatus.PUBLISHED, ModerationStatus = ModerationStatus.APPROVED, LockStatus = ChapterLockStatus.FREE, LanguageId = 1 },
      new Chapter { ChapterId = 2000, SeriesId = 200, ChapterNumber = 1, Title = "B1", ContentType = ContentType.TEXT, Status = ChapterStatus.PUBLISHED, ModerationStatus = ModerationStatus.APPROVED, LockStatus = ChapterLockStatus.FREE, LanguageId = 1 });
    db.Transactions.AddRange(
      new Transaction { TransactionId = 1, UserId = 1, Type = TransactionType.PURCHASE_COIN, Status = TransactionStatus.COMPLETED, AmountCoins = 100, CreatedAt = DateTime.UtcNow },
      new Transaction { TransactionId = 2, UserId = 1, Type = TransactionType.PURCHASE_COIN, Status = TransactionStatus.COMPLETED, AmountCoins = 80, CreatedAt = DateTime.UtcNow });
    db.ChapterUnlocks.AddRange(
      new ChapterUnlock { UnlockId = 1, UserId = 1, ChapterId = 1000, TransactionId = 1, CoinsPaid = 50, UnlockSource = UnlockSource.COIN },
      new ChapterUnlock { UnlockId = 2, UserId = 1, ChapterId = 2000, TransactionId = 2, CoinsPaid = 30, UnlockSource = UnlockSource.COIN });
    await db.SaveChangesAsync();

    var service = new FinancialReportService(db);
    var output = await service.GetSummaryAsync(new FinancialReportRequest
    {
      From = DateTime.UtcNow.AddDays(-1),
      To = DateTime.UtcNow.AddDays(1),
      TopCreators = 1
    });

    output.TopCreators.Should().HaveCount(1);
    output.TopCreators[0].CreatorId.Should().Be(10);
  }
}
