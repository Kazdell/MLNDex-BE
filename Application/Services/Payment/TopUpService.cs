using Application.DTOs.Payment;
using Application.DTOs.Request;
using Application.DTOs.System;
using Application.Interfaces;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Threading;

namespace Application.Services;

public class TopUpService : ITopUpService
{
  private readonly IMlndexDbContext _context;
  private readonly IPaymentGatewayFactory _gatewayFactory;
  private readonly ILogger<TopUpService> _logger;

  public TopUpService(
      IMlndexDbContext context,
      IPaymentGatewayFactory gatewayFactory,
      ILogger<TopUpService> logger,
      IConfiguration configuration)
  {
    _context = context;
    _gatewayFactory = gatewayFactory;
    _logger = logger;
  }



  public async Task<SystemConfigDto> GetCoinRateAsync(CancellationToken cancellationToken = default)
  {
    var config = await _context.SystemConfigs.FirstOrDefaultAsync(cancellationToken)
        ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Chưa có cấu hình hệ thống nào được thiết lập.");

    return new SystemConfigDto
    {
      ExchangeRateCoinToVnd = config.ExchangeRateCoinToVnd,
      WithdrawalFeePercent = config.WithdrawalFeePercent,
      WithdrawalMinCoins = config.WithdrawalMinCoins,
      WithdrawalMaxCoins = config.WithdrawalMaxCoins,
      BlacklistWords = string.IsNullOrEmpty(config.BlacklistWordsJson)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(config.BlacklistWordsJson) ?? new List<string>()
    };
  }

  public async Task<List<CoinPackageResponseDto>> GetActivePackagesAsync()
  {
    return await _context.CoinPackages
        .Where(p => p.IsActive)
        .OrderBy(p => p.PriceVnd)
        .Select(p => new CoinPackageResponseDto
        {
          PackageId = p.PackageId,
          Name = p.Name,
          PriceVnd = p.PriceVnd,
          CoinAmount = p.CoinAmount,
          BonusCoins = p.BonusCoins,
          IsActive = p.IsActive
        })
        .ToListAsync();
  }
  // Thêm ví mới nếu người dùng chưa có ví.
  public async Task<WalletResponseDto> GetWalletAsync(int userId)
  {
    var wallet = await _context.Wallets
        .FirstOrDefaultAsync(w => w.UserId == userId);

    if (wallet == null)
    {
      wallet = new Wallet
      {
        UserId = userId,
        CoinBalance = 0,
        TotalEarned = 0,
        TotalSpent = 0
      };

      _context.Wallets.Add(wallet);
      await _context.SaveChangesAsync();
    }

    return new WalletResponseDto
    {
      WalletId = wallet.WalletId,
      CoinBalance = wallet.CoinBalance,
      TotalEarned = wallet.TotalEarned,
      TotalSpent = wallet.TotalSpent
    };
  }

  public async Task<TransactionPagedResponseDto> GetTransactionHistoryAsync(
      int userId, int page = 1, int pageSize = 20)
  {
    var query = _context.Transactions
        .Where(t => t.UserId == userId)
        .OrderByDescending(t => t.CreatedAt);

    var total = await query.CountAsync();
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(t => new TransactionResponseDto
        {
          TransactionId = t.TransactionId,
          Type = t.Type.ToString(),
          CoinAmount = t.AmountCoins,
          Status = t.Status.ToString(),
          Description = t.Note,
          CreatedAt = t.CreatedAt
        })
        .ToListAsync();

    return new TransactionPagedResponseDto
    {
      Items = items,
      TotalCount = total,
      Page = page,
      PageSize = pageSize
    };
  }



  public async Task<TopUpInitResponseDto> InitiateAsync(int userId, CreateTopUpRequestDto request)
  {
    var method = request.PaymentMethod.ToUpper();

    var rate = await _context.SystemConfigs.FirstOrDefaultAsync()
        ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Chưa có cấu hình hệ thống nào được thiết lập.");

    long amountVnd;
    long coinsWillReceive;
    if (request.PackageId.HasValue)
    {
      var package = await _context.CoinPackages
          .FirstOrDefaultAsync(p => p.PackageId == request.PackageId && p.IsActive)
          ?? throw new KeyNotFoundException("Gói coin không tồn tại hoặc đã ngừng bán.");
      amountVnd = (long)package.PriceVnd;
      coinsWillReceive = (long)(package.CoinAmount + package.BonusCoins);
    }
    else
    {
      amountVnd = request.CustomAmountVnd!.Value;
      if (amountVnd < (long)rate.WithdrawalMinCoins)
        throw new ArgumentException($"Số tiền tối thiểu là {rate.WithdrawalMinCoins:N0} VND.");
      if (amountVnd > (long)rate.WithdrawalMaxCoins)
        throw new ArgumentException($"Số tiền tối đa là {rate.WithdrawalMaxCoins:N0} VND.");
      coinsWillReceive = (long)Math.Floor(amountVnd / rate.ExchangeRateCoinToVnd);
    }

    var wallet = await _context.Wallets
        .FirstOrDefaultAsync(w => w.UserId == userId)
        ?? throw new KeyNotFoundException("Không tìm thấy ví của user.");

    var txnRef = GenerateOrderCode().ToString();
    var expiredAt = DateTime.UtcNow.AddMinutes(15);

    var transaction = new Transaction
    {
      UserId = userId,
      WalletId = wallet.WalletId,
      Type = TransactionType.PURCHASE_COIN,
      AmountCoins = coinsWillReceive,
      Status = TransactionStatus.PENDING,
      Note = $"{method}|{txnRef}",
      CreatedAt = DateTime.UtcNow
    };
    _context.Transactions.Add(transaction);
    await _context.SaveChangesAsync();

    var user = await _context.Users.FindAsync(userId);
    var gateway = _gatewayFactory.GetGateway(method);
    var gatewayResult = await gateway.CreatePaymentAsync(new GatewayCreateRequest
    {
      TxnRef = txnRef,
      AmountVnd = amountVnd,
      Description = $"Nap {coinsWillReceive} coins",
      ReturnUrl = request.ReturnUrl,
      CancelUrl = request.CancelUrl ?? request.ReturnUrl,
      BuyerName = user?.Username ?? string.Empty,
      BuyerEmail = user?.Email ?? string.Empty,
      IpAddress = request.IpAddress
    });

    if (!gatewayResult.IsSuccess)
      throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.VALIDATION_ERROR, $"Tạo link thanh toán thất bại: {gatewayResult.ErrorMessage}");

    return new TopUpInitResponseDto
    {
      TxnRef = txnRef,
      PaymentMethod = method,
      AmountVnd = amountVnd,
      CoinsWillReceive = coinsWillReceive,
      ExpiredAt = expiredAt,
      PaymentUrl = gatewayResult.PaymentUrl
    };
  }

    // Người dùng cancel hoặc error
    transaction.Status = TransactionStatus.FAILED;
    await _context.SaveChangesAsync();

    return new TopUpCallbackResponseDto
    {
      TxnRef = callback.TxnRef,
      Status = "failed",
      Message = $"Thanh toán thất bại hoặc bị huỷ."
    };
  }

  public async Task<TopUpCallbackResponseDto> HandlePayOsWebhookAsync(PayOsWebhookData webhookData)
  {
    var gateway = _gatewayFactory.GetGateway("PAYOS");
    var callback = await gateway.ParseAndVerifyCallbackAsync(webhookData);
    return await ProcessCallbackAsync(callback);
  }

  public async Task<TopUpCallbackResponseDto> HandleVNPayCallbackAsync(VNPayCallbackDto dto)
  {
    var gateway = _gatewayFactory.GetGateway("VNPAY");
    var callback = await gateway.ParseAndVerifyCallbackAsync(dto);
    return await ProcessCallbackAsync(callback);
  }

  public async Task<TopUpCallbackResponseDto> HandleMoMoCallbackAsync(MoMoCallbackDto dto)
  {
    var gateway = _gatewayFactory.GetGateway("MOMO");
    var callback = await gateway.ParseAndVerifyCallbackAsync(dto);
    return await ProcessCallbackAsync(callback);
  }

    _logger.LogInformation("[TopUp] Completed. TxnRef={TxnRef} Coins={Coins} UserId={UserId}",
        txnRef, transaction.AmountCoins, transaction.UserId);

    return new TopUpCallbackResponseDto
    {
      TxnRef = txnRef,
      Status = "success",
      CoinsAdded = (long)transaction.AmountCoins,
      NewBalance = wallet.CoinBalance,
      Message = $"Nạp thành công {transaction.AmountCoins:N0} coins."
    };
  }

  private async Task<TopUpCallbackResponseDto> ProcessCallbackAsync(PaymentCallbackDto callback)
  {
    if (!callback.IsSignatureValid)
    {
      _logger.LogWarning("[TopUp] Signature không hợp lệ. Gateway={Gateway} TxnRef={TxnRef}",
          callback.Gateway, callback.TxnRef);
      return new TopUpCallbackResponseDto
      {
        TxnRef = callback.TxnRef,
        Status = "failed",
        Message = "Signature không hợp lệ."
      };
    }

    var transaction = await FindTransactionAsync(callback.Gateway, callback.TxnRef);
    if (transaction == null)
    {
      _logger.LogWarning("[TopUp] Không tìm thấy transaction. TxnRef={TxnRef}", callback.TxnRef);
      return new TopUpCallbackResponseDto
      {
        TxnRef = callback.TxnRef,
        Status = "failed",
        Message = "Transaction không tồn tại."
      };
    }

    // Nếu webhook đã xử lý thành công trước (ví dụ PayOS vừa gọi server-to-server xong user mới redirect về)
    if (transaction.Status == TransactionStatus.COMPLETED)
    {
      return new TopUpCallbackResponseDto
      {
        TxnRef = callback.TxnRef,
        Status = "success",
        CoinsAdded = (long)transaction.AmountCoins,
        Message = $"Giao dịch đã được ghi nhận thành công từ trước."
      };
    }

    if (transaction.Status == TransactionStatus.FAILED)
    {
      return new TopUpCallbackResponseDto
      {
        TxnRef = callback.TxnRef,
        Status = "failed",
        Message = "Giao dịch đã bị huỷ hoặc thất bại trước đó."
      };
    }

    if (transaction.Status == TransactionStatus.REFUNDED)
    {
      return new TopUpCallbackResponseDto
      {
        TxnRef = callback.TxnRef,
        Status = "refunded",
        Message = "Giao dịch đã được hoàn tiền trước đó."
      };
    }

    // Đang PENDING thì ta tiếp tục xử lý
    if (callback.Status == "PAID")
      return await CompleteTopUpAsync(transaction, callback.TxnRef);

    // Người dùng cancel hoặc error
    transaction.Status = TransactionStatus.FAILED;
    await _context.SaveChangesAsync();

    return new TopUpCallbackResponseDto
    {
      TxnRef = callback.TxnRef,
      Status = "failed",
      Message = $"Thanh toán thất bại hoặc bị huỷ."
    };
  }

  private async Task<TopUpCallbackResponseDto> CompleteTopUpAsync(Transaction transaction, string txnRef)
  {
    var wallet = await _context.Wallets.FindAsync(transaction.WalletId)
        ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Không tìm thấy ví.");

    wallet.CoinBalance += transaction.AmountCoins;
    wallet.TotalEarned += transaction.AmountCoins;
    transaction.Status = TransactionStatus.COMPLETED;

    await _context.SaveChangesAsync();

    _logger.LogInformation("[TopUp] Completed. TxnRef={TxnRef} Coins={Coins} UserId={UserId}",
        txnRef, transaction.AmountCoins, transaction.UserId);

    return new TopUpCallbackResponseDto
    {
      TxnRef = txnRef,
      Status = "success",
      CoinsAdded = (long)transaction.AmountCoins,
      NewBalance = wallet.CoinBalance,
      Message = $"Nạp thành công {transaction.AmountCoins:N0} coins."
    };
  }

  private async Task<Transaction?> FindTransactionAsync(string gateway, string txnRef)
  {
    var notePrefix = $"{gateway.ToUpper()}|{txnRef}";
    _logger.LogInformation("[TopUp] Tìm transaction. NotePrefix={NotePrefix}", notePrefix);
    return await _context.Transactions
        .FirstOrDefaultAsync(t =>
            t.Type == TransactionType.PURCHASE_COIN &&
            t.Note != null && t.Note.StartsWith(notePrefix));
  }

  private static long GenerateOrderCode()
  {
    var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    var random = Random.Shared.Next(100, 999);
    return long.Parse($"{timestamp}{random}");
  }
}
