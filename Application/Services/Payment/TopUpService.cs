using Application.DTOs.Payment;
using Application.DTOs.Request;
using Application.Interfaces;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

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

	// ────────────────────────────────────────────────
	// Queries
	// ────────────────────────────────────────────────

	public async Task<CoinRateResponseDto> GetCoinRateAsync()
	{
		var rate = await _context.CoinRateSettings
			.Where(r => r.IsActive)
			.FirstOrDefaultAsync()
			?? throw new InvalidOperationException("Chưa có tỷ giá coin nào được cấu hình.");

		return new CoinRateResponseDto
		{
			CoinsPerVnd = rate.CoinsPerVnd,
			MinTopUpVnd = rate.MinTopUpVnd,
			MaxTopUpVnd = rate.MaxTopUpVnd
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

    public async Task<WalletResponseDto> GetWalletAsync(int userId)
    {
        // Sử dụng tên chuẩn trong DbContext của bạn (Wallet không có 's')
        var wallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId);

        // Nếu không tìm thấy dữ liệu trong DB
        if (wallet == null)
        {
            // Khởi tạo ví mới để tránh lỗi KeyNotFoundException
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

	// ────────────────────────────────────────────────
	// Initiate top-up
	// ────────────────────────────────────────────────

	public async Task<TopUpInitResponseDto> InitiateAsync(int userId, CreateTopUpRequestDto request)
	{
		var method = request.PaymentMethod.ToUpper();

		var rate = await _context.CoinRateSettings
			.Where(r => r.IsActive)
			.FirstOrDefaultAsync()
			?? throw new InvalidOperationException("Chưa có tỷ giá coin nào được cấu hình.");

		// Tính amountVnd và coins
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

			if (amountVnd < rate.MinTopUpVnd)
				throw new ArgumentException($"Số tiền tối thiểu là {rate.MinTopUpVnd:N0} VND.");
			if (amountVnd > rate.MaxTopUpVnd)
				throw new ArgumentException($"Số tiền tối đa là {rate.MaxTopUpVnd:N0} VND.");

			coinsWillReceive = (long)Math.Floor(amountVnd * rate.CoinsPerVnd);
		}

		var wallet = await _context.Wallets
			.FirstOrDefaultAsync(w => w.UserId == userId)
			?? throw new KeyNotFoundException("Không tìm thấy ví của user.");

		var txnRef = GenerateOrderCode().ToString();
		var expiredAt = DateTime.UtcNow.AddMinutes(15);

		// Tạo Transaction PENDING
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

		// PAYOS / VNPAY / MOMO: gọi gateway tạo link
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
			throw new Exception($"Tạo link thanh toán thất bại: {gatewayResult.ErrorMessage}");

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

	// ────────────────────────────────────────────────
	// Callbacks
	// ────────────────────────────────────────────────

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

	// ────────────────────────────────────────────────
	// Private helpers
	// ────────────────────────────────────────────────

	/// <summary>
	/// Xử lý callback đã chuẩn hoá — dùng chung cho mọi cổng.
	/// Idempotent: gọi nhiều lần cùng TxnRef vẫn an toàn.
	/// </summary>
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

		var transaction = await FindPendingTransactionAsync(callback.Gateway, callback.TxnRef);
		if (transaction == null)
		{
			_logger.LogWarning("[TopUp] Không tìm thấy PENDING transaction. TxnRef={TxnRef}", callback.TxnRef);
			return new TopUpCallbackResponseDto
			{
				TxnRef = callback.TxnRef,
				Status = "failed",
				Message = "Transaction không tồn tại hoặc đã xử lý."
			};
		}

		if (callback.Status == "PAID")
			return await CompleteTopUpAsync(transaction, callback.TxnRef);

		// CANCELLED hoặc FAILED
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
			?? throw new InvalidOperationException("Không tìm thấy ví.");

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

	private async Task<Transaction?> FindPendingTransactionAsync(string gateway, string txnRef)
	{
		var notePrefix = $"{gateway.ToUpper()}|{txnRef}";
		_logger.LogInformation("[TopUp] Tìm transaction. NotePrefix={NotePrefix}", notePrefix);
		return await _context.Transactions
			.FirstOrDefaultAsync(t =>
				t.Type == TransactionType.PURCHASE_COIN &&
				t.Status == TransactionStatus.PENDING &&
				t.Note != null && t.Note.StartsWith(notePrefix));
	}

	private static long GenerateOrderCode()
	{
		var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
		var random = Random.Shared.Next(100, 999);
		return long.Parse($"{timestamp}{random}");
	}
}