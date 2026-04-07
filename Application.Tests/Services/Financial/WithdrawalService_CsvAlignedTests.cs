using System;
using System.Threading.Tasks;
using Application.DTOs.Financial;
using Application.Services.Financial;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Financial;

public class WithdrawalService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public WithdrawalService_CsvAlignedTests(ITestOutputHelper output)
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
  public async Task GetPendingAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();

    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 11, PenName = "Creator One", UserId = 1 });
    db.WithdrawalRequests.Add(new WithdrawalRequest
    {
      WithdrawalId = 1,
      CreatorId = 11,
      AmountCoins = 500,
      AmountVnd = 100000,
      BankAccountInfo = "ABC-123",
      RequestedAt = DateTime.UtcNow,
      Status = WithdrawalStatus.PENDING
    });
    await db.SaveChangesAsync();

    var service = new WithdrawalService(db, NullLogger<WithdrawalService>.Instance);

    var output = await service.GetPendingAsync(new WithdrawalReviewListRequest
    {
      Page = 1,
      PageSize = 10
    });

    _output.WriteLine("Input: one pending withdrawal request");
    _output.WriteLine($"Output: count={output.Items.Count}, total={output.TotalCount}");

    output.Items.Should().HaveCount(1);
    output.TotalCount.Should().Be(1);
  }

  [Fact]
  public async Task GetPendingAsync_TC02_Empty_WhenOnlyCompletedRequestsExist()
  {
    await using var db = CreateInMemoryDbContext();

    db.CreatorProfiles.Add(new CreatorProfile { CreatorId = 11, PenName = "Creator One", UserId = 1 });
    db.WithdrawalRequests.Add(new WithdrawalRequest
    {
      WithdrawalId = 2,
      CreatorId = 11,
      AmountCoins = 500,
      AmountVnd = 100000,
      BankAccountInfo = "ABC-123",
      RequestedAt = DateTime.UtcNow,
      Status = WithdrawalStatus.COMPLETED
    });
    await db.SaveChangesAsync();

    var service = new WithdrawalService(db, NullLogger<WithdrawalService>.Instance);
    var output = await service.GetPendingAsync(new WithdrawalReviewListRequest { Page = 1, PageSize = 10 });

    output.Items.Should().BeEmpty();
    output.TotalCount.Should().Be(0);
  }

  [Fact]
  public async Task GetPendingAsync_TC03_BusinessRule_FilterByCreatorId()
  {
    await using var db = CreateInMemoryDbContext();

    db.CreatorProfiles.AddRange(
      new CreatorProfile { CreatorId = 11, PenName = "Creator One", UserId = 1 },
      new CreatorProfile { CreatorId = 12, PenName = "Creator Two", UserId = 2 });
    db.WithdrawalRequests.AddRange(
      new WithdrawalRequest
      {
        WithdrawalId = 3,
        CreatorId = 11,
        AmountCoins = 500,
        AmountVnd = 100000,
        BankAccountInfo = "A",
        RequestedAt = DateTime.UtcNow.AddMinutes(-1),
        Status = WithdrawalStatus.PENDING
      },
      new WithdrawalRequest
      {
        WithdrawalId = 4,
        CreatorId = 12,
        AmountCoins = 700,
        AmountVnd = 130000,
        BankAccountInfo = "B",
        RequestedAt = DateTime.UtcNow,
        Status = WithdrawalStatus.PENDING
      });
    await db.SaveChangesAsync();

    var service = new WithdrawalService(db, NullLogger<WithdrawalService>.Instance);
    var output = await service.GetPendingAsync(new WithdrawalReviewListRequest
    {
      Page = 1,
      PageSize = 10,
      CreatorId = 11
    });

    output.Items.Should().HaveCount(1);
    output.Items[0].CreatorId.Should().Be(11);
  }
}
