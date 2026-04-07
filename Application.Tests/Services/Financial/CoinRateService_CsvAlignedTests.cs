using System;
using System.Threading.Tasks;
using Application.DTOs.Payment;
using Application.Services.Payment;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Financial;

public class CoinRateService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public CoinRateService_CsvAlignedTests(ITestOutputHelper output)
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

  private static CoinRateService CreateService(MlndexDbContext db)
    => new(db, NullLogger<CoinRateService>.Instance);

  [Fact]
  public async Task GetActiveRateAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.Add(new CoinRateSetting
    {
      Id = 1,
      CoinsPerVnd = 0.01m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 500000,
      IsActive = true,
      UpdatedAt = DateTime.UtcNow,
      UpdatedByUserId = 1,
      Note = "base"
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetActiveRateAsync();

    _output.WriteLine("Input: one active rate");
    _output.WriteLine($"Output: CoinsPerVnd={output.CoinsPerVnd}, Min={output.MinTopUpVnd}");

    output.CoinsPerVnd.Should().Be(0.01m);
    output.MinTopUpVnd.Should().Be(1000);
  }

  [Fact]
  public async Task GetActiveRateAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.AddRange(
      new CoinRateSetting { Id = 1, CoinsPerVnd = 0.01m, MinTopUpVnd = 1000, MaxTopUpVnd = 100000, IsActive = true, UpdatedAt = DateTime.UtcNow.AddMinutes(-10), UpdatedByUserId = 1 },
      new CoinRateSetting { Id = 2, CoinsPerVnd = 0.02m, MinTopUpVnd = 2000, MaxTopUpVnd = 200000, IsActive = true, UpdatedAt = DateTime.UtcNow, UpdatedByUserId = 1 });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetActiveRateAsync();

    _output.WriteLine("Input: two active rates (data quality issue)");
    _output.WriteLine($"Output: CoinsPerVnd={output.CoinsPerVnd}");

    output.Should().NotBeNull();
  }

  [Fact]
  public async Task GetActiveRateAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetActiveRateAsync());

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetHistoryAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.AddRange(
      new CoinRateSetting { Id = 1, CoinsPerVnd = 0.01m, MinTopUpVnd = 1000, MaxTopUpVnd = 100000, IsActive = false, UpdatedAt = DateTime.UtcNow.AddDays(-2), UpdatedByUserId = 1 },
      new CoinRateSetting { Id = 2, CoinsPerVnd = 0.02m, MinTopUpVnd = 1000, MaxTopUpVnd = 200000, IsActive = true, UpdatedAt = DateTime.UtcNow.AddDays(-1), UpdatedByUserId = 1 });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetHistoryAsync();

    _output.WriteLine("Input: 2 history rows");
    _output.WriteLine($"Output: Count={output.Count}, FirstRate={output[0].CoinsPerVnd}");

    output.Should().HaveCount(2);
    output[0].CoinsPerVnd.Should().Be(0.02m);
  }

  [Fact]
  public async Task GetHistoryAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.GetHistoryAsync();

    _output.WriteLine("Input: empty history table");
    _output.WriteLine($"Output: Count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetHistoryAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetHistoryAsync());

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UpdateRateAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.Add(new CoinRateSetting
    {
      Id = 1,
      CoinsPerVnd = 0.01m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 100000,
      IsActive = true,
      UpdatedAt = DateTime.UtcNow.AddDays(-1),
      UpdatedByUserId = 1
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var input = new UpdateCoinRateDto { CoinsPerVnd = 0.02m, MinTopUpVnd = 2000, MaxTopUpVnd = 200000, Note = "Adjust" };
    var output = await service.UpdateRateAsync(99, input);

    _output.WriteLine("Input: adminUserId=99 update to new active rate");
    _output.WriteLine($"Output: CoinsPerVnd={output.CoinsPerVnd}, Min={output.MinTopUpVnd}");

    output.CoinsPerVnd.Should().Be(0.02m);
    (await db.CoinRateSettings.CountAsync(r => r.IsActive)).Should().Be(1);
  }

  [Fact]
  public async Task UpdateRateAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateRateAsync(1, null!));

    _output.WriteLine("Input: dto=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UpdateRateAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateRateAsync(1, new UpdateCoinRateDto
    {
      CoinsPerVnd = 0.01m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 10000,
      Note = "x"
    }));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UpdateRateAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.UpdateRateAsync(2, new UpdateCoinRateDto
    {
      CoinsPerVnd = 0.015m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 150000,
      Note = "initial"
    });

    _output.WriteLine("Input: no existing active rate");
    _output.WriteLine($"Output: CoinsPerVnd={output.CoinsPerVnd}");

    output.CoinsPerVnd.Should().Be(0.015m);
  }

  [Fact]
  public async Task UpdateRateAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.AddRange(
      new CoinRateSetting { Id = 1, CoinsPerVnd = 0.01m, MinTopUpVnd = 1000, MaxTopUpVnd = 100000, IsActive = true, UpdatedAt = DateTime.UtcNow.AddDays(-2), UpdatedByUserId = 1 },
      new CoinRateSetting { Id = 2, CoinsPerVnd = 0.02m, MinTopUpVnd = 1000, MaxTopUpVnd = 100000, IsActive = true, UpdatedAt = DateTime.UtcNow.AddDays(-1), UpdatedByUserId = 1 });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    await service.UpdateRateAsync(7, new UpdateCoinRateDto
    {
      CoinsPerVnd = 0.03m,
      MinTopUpVnd = 3000,
      MaxTopUpVnd = 300000,
      Note = "normalize"
    });

    var activeCount = await db.CoinRateSettings.CountAsync(r => r.IsActive);
    _output.WriteLine("Input: multiple old active rates");
    _output.WriteLine($"Output: ActiveCount={activeCount}");

    activeCount.Should().Be(1);
  }

  [Fact]
  public async Task CalculateCoinsAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.Add(new CoinRateSetting
    {
      Id = 1,
      CoinsPerVnd = 0.01m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 100000,
      IsActive = true,
      UpdatedAt = DateTime.UtcNow,
      UpdatedByUserId = 1
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.CalculateCoinsAsync(10000);

    _output.WriteLine("Input: amountVnd=10000");
    _output.WriteLine($"Output: Coins={output}");

    output.Should().Be(100);
  }

  [Fact]
  public async Task CalculateCoinsAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.Add(new CoinRateSetting
    {
      Id = 1,
      CoinsPerVnd = 0.01m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 100000,
      IsActive = true,
      UpdatedAt = DateTime.UtcNow,
      UpdatedByUserId = 1
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.CalculateCoinsAsync(-5000);

    _output.WriteLine("Input: amountVnd=-5000");
    _output.WriteLine($"Output: Coins={output}");

    output.Should().Be(-50);
  }

  [Fact]
  public async Task CalculateCoinsAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.CalculateCoinsAsync(1000));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task CalculateCoinsAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CalculateCoinsAsync(1000));

    _output.WriteLine("Input: amountVnd=1000 without active rate");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Chưa có tỷ giá coin nào được cấu hình.");
  }

  [Fact]
  public async Task CalculateCoinsAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.Add(new CoinRateSetting
    {
      Id = 1,
      CoinsPerVnd = 0.01m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 100000,
      IsActive = true,
      UpdatedAt = DateTime.UtcNow,
      UpdatedByUserId = 1
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.CalculateCoinsAsync(1999);

    _output.WriteLine("Input: amountVnd=1999");
    _output.WriteLine($"Output: Coins={output}");

    output.Should().Be(19);
  }
}
