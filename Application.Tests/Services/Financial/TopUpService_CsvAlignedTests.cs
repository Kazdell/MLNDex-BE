using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Payment;
using Application.DTOs.Request;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using FluentAssertions;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Application.Tests.Services.Financial;

public class TopUpService_CsvAlignedTests
{
  private readonly ITestOutputHelper _output;
  private readonly Mock<IPaymentGatewayFactory> _mockGatewayFactory = new();
  private readonly Mock<IPaymentGatewayService> _mockGateway = new();

  public TopUpService_CsvAlignedTests(ITestOutputHelper output)
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

  private static IConfiguration CreateConfiguration()
    => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

  private TopUpService CreateService(MlndexDbContext db)
  {
    _mockGatewayFactory.Setup(x => x.GetGateway("PAYOS")).Returns(_mockGateway.Object);
    _mockGatewayFactory.Setup(x => x.GetGateway("VNPAY")).Returns(_mockGateway.Object);
    _mockGatewayFactory.Setup(x => x.GetGateway("MOMO")).Returns(_mockGateway.Object);

    _mockGateway
      .Setup(x => x.CreatePaymentAsync(It.IsAny<GatewayCreateRequest>()))
      .ReturnsAsync(GatewayCreateResult.Success("https://gateway.test/pay"));

    return new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
  }

  private static async Task SeedUserWalletRateAsync(MlndexDbContext db, int userId = 1)
  {
    db.Users.Add(new User
    {
      UserId = userId,
      Username = "wallet_user",
      Email = "wallet@test.com",
      DisplayName = "Wallet User",
      PasswordHash = "hash",
      IsActive = true,
      IsEmailVerified = true,
      CreatedAt = DateTime.UtcNow
    });

    db.Wallets.Add(new Wallet
    {
      WalletId = 10,
      UserId = userId,
      CoinBalance = 100,
      TotalEarned = 500,
      TotalSpent = 400
    });

    db.CoinRateSettings.Add(new CoinRateSetting
    {
      Id = 1,
      CoinsPerVnd = 0.01m,
      MinTopUpVnd = 1000,
      MaxTopUpVnd = 500000,
      IsActive = true,
      UpdatedByUserId = userId,
      UpdatedAt = DateTime.UtcNow,
      Note = "base"
    });

    db.CoinPackages.AddRange(
      new CoinPackage { PackageId = 1, Name = "Starter", CoinAmount = 100, BonusCoins = 10, PriceVnd = 10000, IsActive = true, CreatedAt = DateTime.UtcNow },
      new CoinPackage { PackageId = 2, Name = "Hidden", CoinAmount = 300, BonusCoins = 30, PriceVnd = 25000, IsActive = false, CreatedAt = DateTime.UtcNow });

    await db.SaveChangesAsync();
  }

  [Fact]
  public async Task GetCoinRateAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db);
    var service = CreateService(db);

    var output = await service.GetCoinRateAsync();

    _output.WriteLine("Input: active rate exists");
    _output.WriteLine($"Output: CoinsPerVnd={output.CoinsPerVnd}, Min={output.MinTopUpVnd}, Max={output.MaxTopUpVnd}");

    output.CoinsPerVnd.Should().Be(0.01m);
    output.MinTopUpVnd.Should().Be(1000);
  }

  [Fact]
  public async Task GetCoinRateAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.AddRange(
      new CoinRateSetting { Id = 1, CoinsPerVnd = 0.01m, MinTopUpVnd = 1000, MaxTopUpVnd = 100000, IsActive = true, UpdatedByUserId = 1, UpdatedAt = DateTime.UtcNow.AddMinutes(-10) },
      new CoinRateSetting { Id = 2, CoinsPerVnd = 0.02m, MinTopUpVnd = 2000, MaxTopUpVnd = 200000, IsActive = true, UpdatedByUserId = 1, UpdatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetCoinRateAsync();

    _output.WriteLine("Input: two active rates (data quality issue)");
    _output.WriteLine($"Output: CoinsPerVnd={output.CoinsPerVnd}");

    output.Should().NotBeNull();
  }

  [Fact]
  public async Task GetCoinRateAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetCoinRateAsync());

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetCoinRateAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetCoinRateAsync());

    _output.WriteLine("Input: no active rate");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Chưa có tỷ giá coin nào được cấu hình.");
  }

  [Fact]
  public async Task GetCoinRateAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    db.CoinRateSettings.AddRange(
      new CoinRateSetting { Id = 1, CoinsPerVnd = 0.01m, MinTopUpVnd = 1000, MaxTopUpVnd = 100000, IsActive = false, UpdatedByUserId = 1, UpdatedAt = DateTime.UtcNow },
      new CoinRateSetting { Id = 2, CoinsPerVnd = 0.03m, MinTopUpVnd = 3000, MaxTopUpVnd = 300000, IsActive = true, UpdatedByUserId = 1, UpdatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetCoinRateAsync();

    _output.WriteLine("Input: one inactive + one active");
    _output.WriteLine($"Output: CoinsPerVnd={output.CoinsPerVnd}");

    output.CoinsPerVnd.Should().Be(0.03m);
  }

  [Fact]
  public async Task GetActivePackagesAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db);
    var service = CreateService(db);

    var output = await service.GetActivePackagesAsync();

    _output.WriteLine("Input: one active + one inactive package");
    _output.WriteLine($"Output: Count={output.Count}, First={output[0].Name}");

    output.Should().HaveCount(1);
    output[0].Name.Should().Be("Starter");
  }

  [Fact]
  public async Task GetActivePackagesAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var output = await service.GetActivePackagesAsync();

    _output.WriteLine("Input: empty package table");
    _output.WriteLine($"Output: Count={output.Count}");

    output.Should().BeEmpty();
  }

  [Fact]
  public async Task GetActivePackagesAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetActivePackagesAsync());

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetWalletAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 11);
    var service = CreateService(db);

    var output = await service.GetWalletAsync(11);

    _output.WriteLine("Input: userId=11");
    _output.WriteLine($"Output: WalletId={output.WalletId}, Balance={output.CoinBalance}");

    output.WalletId.Should().Be(10);
    output.CoinBalance.Should().Be(100);
  }

  [Fact]
  public async Task GetWalletAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetWalletAsync(0));

    _output.WriteLine("Input: userId=0");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Không tìm thấy ví của user.");
  }

  [Fact]
  public async Task GetWalletAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetWalletAsync(1));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task GetWalletAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 12);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetWalletAsync(404));

    _output.WriteLine("Input: userId=404");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Không tìm thấy ví của user.");
  }

  [Fact]
  public async Task GetWalletAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 13);

    var wallet = await db.Wallets.FirstAsync(w => w.UserId == 13);
    wallet.CoinBalance = 0;
    wallet.TotalEarned = 0;
    wallet.TotalSpent = 0;
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetWalletAsync(13);

    _output.WriteLine("Input: zero-balance wallet");
    _output.WriteLine($"Output: Balance={output.CoinBalance}");

    output.CoinBalance.Should().Be(0);
  }

  [Fact]
  public async Task GetTransactionHistoryAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 21);
    db.Transactions.AddRange(
      new Transaction { TransactionId = 1, UserId = 21, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 100, Status = TransactionStatus.COMPLETED, Note = "a", CreatedAt = DateTime.UtcNow.AddMinutes(-5) },
      new Transaction { TransactionId = 2, UserId = 21, WalletId = 10, Type = TransactionType.BONUS, AmountCoins = 20, Status = TransactionStatus.COMPLETED, Note = "b", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetTransactionHistoryAsync(21, 1, 20);

    _output.WriteLine("Input: userId=21,page=1,pageSize=20");
    _output.WriteLine($"Output: Total={output.TotalCount}, FirstId={output.Items.First().TransactionId}");

    output.TotalCount.Should().Be(2);
    output.Items.First().TransactionId.Should().Be(2);
  }

  [Fact]
  public async Task GetTransactionHistoryAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 22);
    db.Transactions.Add(new Transaction { TransactionId = 1, UserId = 22, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 100, Status = TransactionStatus.COMPLETED, Note = "a", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    var service = CreateService(db);
    var output = await service.GetTransactionHistoryAsync(22, 0, 20);

    _output.WriteLine("Input: page=0 (invalid)");
    _output.WriteLine($"Output: Total={output.TotalCount}, Count={output.Items.Count()}");

    output.TotalCount.Should().Be(1);
    output.Items.Should().HaveCount(1);
  }

  [Fact]
  public async Task GetTransactionHistoryAsync_TC03_Exception()
  {
    var db = CreateInMemoryDbContext();
    var service = CreateService(db);
    await db.DisposeAsync();

    var ex = await Assert.ThrowsAnyAsync<Exception>(() => service.GetTransactionHistoryAsync(1, 1, 20));

    _output.WriteLine("Input: disposed DbContext");
    _output.WriteLine($"Output Exception Type: {ex.GetType().Name}");

    ex.Should().NotBeNull();
  }

  [Fact]
  public async Task InitiateAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 31);
    var service = CreateService(db);

    var output = await service.InitiateAsync(31, new CreateTopUpRequestDto
    {
      PaymentMethod = "PAYOS",
      PackageId = 1,
      ReturnUrl = "https://client/return",
      CancelUrl = "https://client/cancel",
      IpAddress = "127.0.0.1"
    });

    _output.WriteLine("Input: package top-up via PAYOS");
    _output.WriteLine($"Output: TxnRef={output.TxnRef}, Coins={output.CoinsWillReceive}, Url={output.PaymentUrl}");

    output.CoinsWillReceive.Should().Be(110);
    output.PaymentUrl.Should().Be("https://gateway.test/pay");
    (await db.Transactions.CountAsync(t => t.UserId == 31 && t.Status == TransactionStatus.PENDING)).Should().Be(1);
  }

  [Fact]
  public async Task InitiateAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 32);
    var service = CreateService(db);

    var ex = await Assert.ThrowsAsync<ArgumentException>(() => service.InitiateAsync(32, new CreateTopUpRequestDto
    {
      PaymentMethod = "PAYOS",
      CustomAmountVnd = 500,
      ReturnUrl = "https://client/return",
      IpAddress = "127.0.0.1"
    }));

    _output.WriteLine("Input: custom amount below min");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Số tiền tối thiểu");
  }

  [Fact]
  public async Task InitiateAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 33);

    _mockGatewayFactory.Setup(x => x.GetGateway("PAYOS")).Returns(_mockGateway.Object);
    _mockGateway
      .Setup(x => x.CreatePaymentAsync(It.IsAny<GatewayCreateRequest>()))
      .ReturnsAsync(GatewayCreateResult.Fail("gateway down"));

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());

    var ex = await Assert.ThrowsAsync<Exception>(() => service.InitiateAsync(33, new CreateTopUpRequestDto
    {
      PaymentMethod = "PAYOS",
      PackageId = 1,
      ReturnUrl = "https://client/return",
      IpAddress = "127.0.0.1"
    }));

    _output.WriteLine("Input: gateway create payment fails");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Contain("Tạo link thanh toán thất bại");
  }

  [Fact]
  public async Task HandlePayOsWebhookAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 41);
    db.Transactions.Add(new Transaction { TransactionId = 1, UserId = 41, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 100, Status = TransactionStatus.PENDING, Note = "PAYOS|tx001", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    _mockGatewayFactory.Setup(x => x.GetGateway("PAYOS")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto
    {
      Gateway = "PAYOS",
      TxnRef = "tx001",
      Status = "PAID",
      IsSignatureValid = true
    });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandlePayOsWebhookAsync(new PayOsWebhookData());

    _output.WriteLine("Input: PAYOS callback PAID tx001");
    _output.WriteLine($"Output: Status={output.Status}, NewBalance={output.NewBalance}");

    output.Status.Should().Be("success");
    output.CoinsAdded.Should().Be(100);
  }

  [Fact]
  public async Task HandlePayOsWebhookAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("PAYOS")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto
    {
      Gateway = "PAYOS",
      TxnRef = "tx002",
      Status = "PAID",
      IsSignatureValid = false
    });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandlePayOsWebhookAsync(new PayOsWebhookData());

    _output.WriteLine("Input: invalid signature");
    _output.WriteLine($"Output: Status={output.Status}, Message={output.Message}");

    output.Status.Should().Be("failed");
    output.Message.Should().Be("Signature không hợp lệ.");
  }

  [Fact]
  public async Task HandlePayOsWebhookAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 42);
    db.Wallets.Remove(await db.Wallets.FirstAsync(w => w.UserId == 42));
    db.Transactions.Add(new Transaction { TransactionId = 2, UserId = 42, WalletId = 999, Type = TransactionType.PURCHASE_COIN, AmountCoins = 50, Status = TransactionStatus.PENDING, Note = "PAYOS|tx003", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    _mockGatewayFactory.Setup(x => x.GetGateway("PAYOS")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto
    {
      Gateway = "PAYOS",
      TxnRef = "tx003",
      Status = "PAID",
      IsSignatureValid = true
    });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.HandlePayOsWebhookAsync(new PayOsWebhookData()));

    _output.WriteLine("Input: callback paid but wallet missing");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("Không tìm thấy ví.");
  }

  [Fact]
  public async Task HandlePayOsWebhookAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("PAYOS")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto
    {
      Gateway = "PAYOS",
      TxnRef = "unknown",
      Status = "PAID",
      IsSignatureValid = true
    });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandlePayOsWebhookAsync(new PayOsWebhookData());

    _output.WriteLine("Input: callback for unknown txn");
    _output.WriteLine($"Output: Status={output.Status}, Message={output.Message}");

    output.Status.Should().Be("failed");
    output.Message.Should().Contain("Transaction không tồn tại");
  }

  [Fact]
  public async Task HandlePayOsWebhookAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 43);
    db.Transactions.Add(new Transaction { TransactionId = 3, UserId = 43, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 80, Status = TransactionStatus.PENDING, Note = "PAYOS|tx005", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    _mockGatewayFactory.Setup(x => x.GetGateway("PAYOS")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto
    {
      Gateway = "PAYOS",
      TxnRef = "tx005",
      Status = "FAILED",
      IsSignatureValid = true
    });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandlePayOsWebhookAsync(new PayOsWebhookData());

    _output.WriteLine("Input: callback failed status");
    _output.WriteLine($"Output: Status={output.Status}");

    output.Status.Should().Be("failed");
    (await db.Transactions.FirstAsync(t => t.TransactionId == 3)).Status.Should().Be(TransactionStatus.FAILED);
  }

  [Fact]
  public async Task HandleVNPayCallbackAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 51);
    db.Transactions.Add(new Transaction { TransactionId = 10, UserId = 51, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 60, Status = TransactionStatus.PENDING, Note = "VNPAY|vnp001", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    _mockGatewayFactory.Setup(x => x.GetGateway("VNPAY")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "VNPAY", TxnRef = "vnp001", Status = "PAID", IsSignatureValid = true });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleVNPayCallbackAsync(new VNPayCallbackDto());

    _output.WriteLine("Input: VNPAY PAID callback");
    _output.WriteLine($"Output: Status={output.Status}, Coins={output.CoinsAdded}");

    output.Status.Should().Be("success");
    output.CoinsAdded.Should().Be(60);
  }

  [Fact]
  public async Task HandleVNPayCallbackAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("VNPAY")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "VNPAY", TxnRef = "vnp002", Status = "PAID", IsSignatureValid = false });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleVNPayCallbackAsync(new VNPayCallbackDto());

    _output.WriteLine("Input: VNPAY invalid signature");
    _output.WriteLine($"Output: Status={output.Status}");

    output.Status.Should().Be("failed");
  }

  [Fact]
  public async Task HandleVNPayCallbackAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("VNPAY")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ThrowsAsync(new Exception("parse failed"));

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var ex = await Assert.ThrowsAsync<Exception>(() => service.HandleVNPayCallbackAsync(new VNPayCallbackDto()));

    _output.WriteLine("Input: VNPAY parse throws");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("parse failed");
  }

  [Fact]
  public async Task HandleVNPayCallbackAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("VNPAY")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "VNPAY", TxnRef = "vnp404", Status = "PAID", IsSignatureValid = true });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleVNPayCallbackAsync(new VNPayCallbackDto());

    _output.WriteLine("Input: VNPAY unknown tx");
    _output.WriteLine($"Output: Message={output.Message}");

    output.Status.Should().Be("failed");
  }

  [Fact]
  public async Task HandleVNPayCallbackAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 52);
    db.Transactions.Add(new Transaction { TransactionId = 11, UserId = 52, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 60, Status = TransactionStatus.PENDING, Note = "VNPAY|vnp005", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    _mockGatewayFactory.Setup(x => x.GetGateway("VNPAY")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "VNPAY", TxnRef = "vnp005", Status = "CANCELLED", IsSignatureValid = true });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleVNPayCallbackAsync(new VNPayCallbackDto());

    _output.WriteLine("Input: VNPAY cancelled callback");
    _output.WriteLine($"Output: Status={output.Status}");

    output.Status.Should().Be("failed");
    (await db.Transactions.FirstAsync(t => t.TransactionId == 11)).Status.Should().Be(TransactionStatus.FAILED);
  }

  [Fact]
  public async Task HandleMoMoCallbackAsync_TC01_Success()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 61);
    db.Transactions.Add(new Transaction { TransactionId = 20, UserId = 61, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 70, Status = TransactionStatus.PENDING, Note = "MOMO|mm001", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    _mockGatewayFactory.Setup(x => x.GetGateway("MOMO")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "MOMO", TxnRef = "mm001", Status = "PAID", IsSignatureValid = true });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleMoMoCallbackAsync(new MoMoCallbackDto());

    _output.WriteLine("Input: MOMO PAID callback");
    _output.WriteLine($"Output: Status={output.Status}, Coins={output.CoinsAdded}");

    output.Status.Should().Be("success");
    output.CoinsAdded.Should().Be(70);
  }

  [Fact]
  public async Task HandleMoMoCallbackAsync_TC02_InvalidInput()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("MOMO")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "MOMO", TxnRef = "mm002", Status = "PAID", IsSignatureValid = false });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleMoMoCallbackAsync(new MoMoCallbackDto());

    _output.WriteLine("Input: MOMO invalid signature");
    _output.WriteLine($"Output: Status={output.Status}");

    output.Status.Should().Be("failed");
  }

  [Fact]
  public async Task HandleMoMoCallbackAsync_TC03_Exception()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("MOMO")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ThrowsAsync(new Exception("momo parse error"));

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var ex = await Assert.ThrowsAsync<Exception>(() => service.HandleMoMoCallbackAsync(new MoMoCallbackDto()));

    _output.WriteLine("Input: MOMO parse throws");
    _output.WriteLine($"Output Exception: {ex.Message}");

    ex.Message.Should().Be("momo parse error");
  }

  [Fact]
  public async Task HandleMoMoCallbackAsync_TC04_NotFound()
  {
    await using var db = CreateInMemoryDbContext();
    _mockGatewayFactory.Setup(x => x.GetGateway("MOMO")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "MOMO", TxnRef = "mm404", Status = "PAID", IsSignatureValid = true });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleMoMoCallbackAsync(new MoMoCallbackDto());

    _output.WriteLine("Input: MOMO unknown tx");
    _output.WriteLine($"Output: Status={output.Status}");

    output.Status.Should().Be("failed");
  }

  [Fact]
  public async Task HandleMoMoCallbackAsync_TC05_BusinessRule()
  {
    await using var db = CreateInMemoryDbContext();
    await SeedUserWalletRateAsync(db, 62);
    db.Transactions.Add(new Transaction { TransactionId = 21, UserId = 62, WalletId = 10, Type = TransactionType.PURCHASE_COIN, AmountCoins = 45, Status = TransactionStatus.PENDING, Note = "MOMO|mm005", CreatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();

    _mockGatewayFactory.Setup(x => x.GetGateway("MOMO")).Returns(_mockGateway.Object);
    _mockGateway.Setup(x => x.ParseAndVerifyCallbackAsync(It.IsAny<object>())).ReturnsAsync(new PaymentCallbackDto { Gateway = "MOMO", TxnRef = "mm005", Status = "FAILED", IsSignatureValid = true });

    var service = new TopUpService(db, _mockGatewayFactory.Object, NullLogger<TopUpService>.Instance, CreateConfiguration());
    var output = await service.HandleMoMoCallbackAsync(new MoMoCallbackDto());

    _output.WriteLine("Input: MOMO failed callback");
    _output.WriteLine($"Output: Status={output.Status}");

    output.Status.Should().Be("failed");
    (await db.Transactions.FirstAsync(t => t.TransactionId == 21)).Status.Should().Be(TransactionStatus.FAILED);
  }
}
