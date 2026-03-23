using Application.DTOs.Payment;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Payment
{
	public interface ICoinRateService
	{
		/// <summary>
		/// Lấy tỷ giá đang active.
		/// Dùng cho cả frontend preview và backend tính toán khi nạp tiền.
		/// </summary>
		Task<CoinRateResponseDto> GetActiveRateAsync();

		/// <summary>
		/// Lịch sử thay đổi tỷ giá — Admin xem audit trail.
		/// Sắp xếp mới nhất lên đầu.
		/// </summary>
		Task<List<CoinRateResponseDto>> GetHistoryAsync();

		/// <summary>
		/// Tạo rate mới, deactivate rate cũ trong 1 transaction (BR-08).
		/// Note bắt buộc — Admin phải ghi lý do thay đổi.
		/// </summary>
		Task<CoinRateResponseDto> UpdateRateAsync(int adminUserId, UpdateCoinRateDto dto);

		/// <summary>
		/// Tính số coins từ VND theo rate đang active.
		/// Dùng để preview trước khi submit.
		/// </summary>
		Task<long> CalculateCoinsAsync(long amountVnd);
	}
}
