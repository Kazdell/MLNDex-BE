using System;
using System.Collections.Generic;
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

public class CoinPackageService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;

  public CoinPackageService_CsvAlignedTests(ITestOutputHelper output)
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

  private static CoinPackageService CreateService(MlndexDbContext db)
    => new(db, NullLogger<CoinPackageService>.Instance);

  [Fact]
  public async Task GetAllAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinPackages.AddRange(
      new CoinPackage { PackageId = 1, Name = "Starter", CoinAmount = 100, BonusCoins = 0, PriceVnd = 10000, IsActive = true, CreatedAt = DateTime.UtcNow },
      new CoinPackage { PackageId = 2, Name = "Pro", CoinAmount = 300, BonusCoins = 20, PriceVnd = 25000, IsActive = true, CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetAllAsync();

    _output.WriteLine("Input: activeOnly=false");
    _output.WriteLine($"Output: Count={output.Count}, First={output[0].Name}");

    output.Should().HaveCount(2);
    output[0].Name.Should().Be("Starter");
  }

  [Fact]
  public async Task GetAllAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.GetAllAsync();

    _output.WriteLine("Input: empty table");
    _output.WriteLine($"Output: Count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetAllAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetAllAsync());

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetByIdAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinPackages.Add(new CoinPackage
    {
      PackageId = 7,
      Name = "Gold",
      CoinAmount = 700,
      BonusCoins = 50,
      PriceVnd = 50000,
      IsActive = true,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetByIdAsync(7);

    _output.WriteLine("Input: packageId=7");
    _output.WriteLine($"Output: Name={output?.Name}, TotalCoins={output?.TotalCoins}");

    output.Should().NotBeNull();
    output!.Name.Should().Be("Gold");
  }

  [Fact]
  public async Task GetByIdAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.GetByIdAsync(0);

    _output.WriteLine("Input: packageId=0");
    _output.WriteLine($"Output: IsNull={output is null}");

    output.Should().BeNull();
  }

  [Fact]
  public async Task GetByIdAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetByIdAsync(1));

    _output.WriteLine("Input: disposed DbContext, packageId=1");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UpdateAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinPackages.Add(new CoinPackage
    {
      PackageId = 11,
      Name = "Pack A",
      CoinAmount = 100,
      BonusCoins = 5,
      PriceVnd = 10000,
      IsActive = true,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var input = new UpdateCoinPackageDto { Name = "Pack A+", PriceVnd = 12000, BonusCoins = 10 };

    var output = await service.UpdateAsync(11, input);

    _output.WriteLine("Input: packageId=11, update Name/Price/Bonus");
    _output.WriteLine($"Output: Name={output.Name}, Price={output.PriceVnd}, Bonus={output.BonusCoins}");

    output.Name.Should().Be("Pack A+");
    output.PriceVnd.Should().Be(12000);
    output.BonusCoins.Should().Be(10);
  }

  [Fact]
  public async Task UpdateAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinPackages.Add(new CoinPackage
    {
      PackageId = 12,
      Name = "Pack B",
      CoinAmount = 200,
      BonusCoins = 0,
      PriceVnd = 20000,
      IsActive = true,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateAsync(12, null!));

    _output.WriteLine("Input: packageId=12, dto=null");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task UpdateAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.UpdateAsync(1, new UpdateCoinPackageDto()));

    _output.WriteLine("Input: disposed DbContext, packageId=1");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task DeactivateAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinPackages.Add(new CoinPackage
    {
      PackageId = 21,
      Name = "Pack D",
      CoinAmount = 500,
      BonusCoins = 50,
      PriceVnd = 45000,
      IsActive = true,
      CreatedAt = DateTime.UtcNow
    });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    await service.DeactivateAsync(21);

    var updated = await db.CoinPackages.FindAsync(21);
    _output.WriteLine("Input: packageId=21");
    _output.WriteLine($"Output: IsActive={updated!.IsActive}");

    updated!.IsActive.Should().BeFalse();
  }

  [Fact]
  public async Task DeactivateAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.DeactivateAsync(0));

    _output.WriteLine("Input: packageId=0");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Không tìm thấy gói coin ID=0.");
  }

  [Fact]
  public async Task DeactivateAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.DeactivateAsync(1));

    _output.WriteLine("Input: disposed DbContext, packageId=1");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }
}
