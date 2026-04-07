using System;
using System.Threading.Tasks;
using Application.Services.System;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.System;

public class SystemConfigService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public SystemConfigService_CsvAlignedTests(ITestOutputHelper output)
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
  public async Task GetAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();

    db.SystemSettings.AddRange(
      new SystemSetting { Key = "ExchangeRateCoinToVnd", Value = "1500", UpdatedAt = DateTime.UtcNow },
      new SystemSetting { Key = "WithdrawalFeePercent", Value = "12", UpdatedAt = DateTime.UtcNow },
      new SystemSetting { Key = "BlacklistWords", Value = "[\"spam\",\"scam\"]", UpdatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new SystemConfigService(db);
    var output = await service.GetAsync();

    _output.WriteLine("Input: configured system settings");
    _output.WriteLine($"Output: rate={output.ExchangeRateCoinToVnd}, fee={output.WithdrawalFeePercent}, blacklistCount={output.BlacklistWords.Count}");

    output.ExchangeRateCoinToVnd.Should().Be(1500);
    output.WithdrawalFeePercent.Should().Be(12);
    output.BlacklistWords.Should().Contain("spam");
  }

  [Fact]
  public async Task GetAsync_TC02_Empty_UsesDefaultValues()
  {
    await using var db = CreateInMemoryDbContext();
    var service = new SystemConfigService(db);

    var output = await service.GetAsync();

    output.ExchangeRateCoinToVnd.Should().Be(1000);
    output.WithdrawalFeePercent.Should().Be(10);
    output.WithdrawalMinCoins.Should().Be(50);
    output.WithdrawalMaxCoins.Should().Be(1000);
    output.BlacklistWords.Should().BeEmpty();
  }

  [Fact]
  public async Task GetAsync_TC03_InvalidInput_InvalidStoredNumbersFallbackToDefaults()
  {
    await using var db = CreateInMemoryDbContext();
    db.SystemSettings.AddRange(
      new SystemSetting { Key = "ExchangeRateCoinToVnd", Value = "bad", UpdatedAt = DateTime.UtcNow },
      new SystemSetting { Key = "WithdrawalFeePercent", Value = "bad", UpdatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = new SystemConfigService(db);
    var output = await service.GetAsync();

    output.ExchangeRateCoinToVnd.Should().Be(1000);
    output.WithdrawalFeePercent.Should().Be(10);
  }
}
