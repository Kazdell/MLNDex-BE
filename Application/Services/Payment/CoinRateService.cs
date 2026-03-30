using Application.DTOs.Payment;
using Application.Interfaces.Data;
using Application.Interfaces.Payment;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Payment;

public class CoinRateService : ICoinRateService
{
	private readonly IMlndexDbContext _context;
	private readonly ILogger<CoinRateService> _logger;

	public CoinRateService(IMlndexDbContext context, ILogger<CoinRateService> logger)
	{
		_context = context;
		_logger = logger;
	}

	/// <summary>
	/// Lấy thiết lập tỷ lệ quy đổi xu hiện đang hoạt động.
	/// </summary>
	public async Task<CoinRateResponseDto> GetActiveRateAsync()
	{
		var rate = await _context.CoinRateSettings
			.Where(r => r.IsActive)
			.FirstOrDefaultAsync()
			?? throw new InvalidOperationException("Chưa có tỷ giá coin nào được cấu hình.");

		return ToDto(rate);
	}

	public async Task<List<CoinRateResponseDto>> GetHistoryAsync()
	{
		return await _context.CoinRateSettings
			.OrderByDescending(r => r.UpdatedAt)
			.Select(r => ToDto(r))
			.ToListAsync();
	}

	/// <summary>
	/// Cập nhật tỷ lệ quy đổi xu mới và vô hiệu hoá các tỷ lệ đang hoạt động trước đó.
	/// </summary>
	public async Task<CoinRateResponseDto> UpdateRateAsync(int adminUserId, UpdateCoinRateDto dto)
	{
		var activeRates = await _context.CoinRateSettings
			.Where(r => r.IsActive)
			.ToListAsync();

		foreach (var old in activeRates)
			old.IsActive = false;

		var newRate = new CoinRateSetting
		{
			CoinsPerVnd = dto.CoinsPerVnd,
			MinTopUpVnd = dto.MinTopUpVnd,
			MaxTopUpVnd = dto.MaxTopUpVnd,
			IsActive = true,
			UpdatedByUserId = adminUserId,
			UpdatedAt = DateTime.UtcNow,
			Note = dto.Note
		};

		_context.CoinRateSettings.Add(newRate);
		await _context.SaveChangesAsync();

		_logger.LogInformation(
			"[CoinRate] Admin {AdminId} cập nhật tỷ giá. CoinsPerVnd={Rate} Note={Note}",
			adminUserId, dto.CoinsPerVnd, dto.Note);

		return ToDto(newRate);
	}

	/// <summary>
	/// Tính toán số xu sẽ nhận được dựa trên số tiền nạp VNĐ theo tỷ lệ quy đổi hiện hành.
	/// </summary>
	public async Task<long> CalculateCoinsAsync(long amountVnd)
	{
		var rate = await _context.CoinRateSettings
			.Where(r => r.IsActive)
			.FirstOrDefaultAsync()
			?? throw new InvalidOperationException("Chưa có tỷ giá coin nào được cấu hình.");

		return (long)Math.Floor(amountVnd * rate.CoinsPerVnd);
	}

	private static CoinRateResponseDto ToDto(CoinRateSetting r) => new()
	{
		CoinsPerVnd = r.CoinsPerVnd,
		MinTopUpVnd = r.MinTopUpVnd,
		MaxTopUpVnd = r.MaxTopUpVnd
	};
}