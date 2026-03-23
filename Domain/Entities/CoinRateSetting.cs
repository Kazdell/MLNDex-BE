using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities;

/// <summary>
/// Config tỉ lệ quy đổi coins do Admin thay đổi qua UI.
/// Mỗi lần update → insert row mới (is_active cũ = false, mới = true)
/// để giữ lịch sử audit đầy đủ.
/// </summary>
public class CoinRateSetting
{
	public int Id { get; set; }

	/// <summary>Coins nhận được trên mỗi VND. VD: 0.1 → 10,000 VND = 1,000 coins.</summary>
	public decimal CoinsPerVnd { get; set; }

	/// <summary>Số tiền VND tối thiểu mỗi giao dịch top-up.</summary>
	public long MinTopUpVnd { get; set; }

	/// <summary>Số tiền VND tối đa mỗi giao dịch top-up.</summary>
	public long MaxTopUpVnd { get; set; }

	/// <summary>Chỉ 1 row có IsActive = true tại mọi thời điểm.</summary>
	public bool IsActive { get; set; }

	public int UpdatedByUserId { get; set; }
	public DateTime UpdatedAt { get; set; }

	/// <summary>Ghi chú lý do thay đổi — Admin điền khi update.</summary>
	public string? Note { get; set; }

	// Navigation
	public User UpdatedBy { get; set; } = null!;
}
