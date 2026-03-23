using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Payment
{
	/// <summary>
	/// Tỉ lệ quy đổi hiện tại — frontend dùng để preview
	/// "nhập X VND → nhận Y coins" realtime trước khi submit.
	/// </summary>
	public class CoinRateResponseDto
	{
		public decimal CoinsPerVnd { get; set; }
		public long MinTopUpVnd { get; set; }
		public long MaxTopUpVnd { get; set; }

		/// <summary>Tính nhanh coins sẽ nhận khi nhập số VND bất kỳ.</summary>
		public long PreviewCoins(long amountVnd)
			=> (long)Math.Floor(amountVnd * CoinsPerVnd);
	}

	/// <summary>Admin cập nhật tỷ giá mới.</summary>
	public class UpdateCoinRateDto : IValidatableObject
	{
		/// <summary>
		/// Coins nhận được trên mỗi VND.
		/// VD: 0.01 → 1,000 VND = 10 coins.
		/// </summary>
		[Range(0.0001, double.MaxValue, ErrorMessage = "Tỷ giá phải lớn hơn 0")]
		public decimal CoinsPerVnd { get; set; }

		[Range(1000, long.MaxValue, ErrorMessage = "Số tiền tối thiểu phải từ 1,000 VND")]
		public long MinTopUpVnd { get; set; }

		[Range(1000, long.MaxValue, ErrorMessage = "Số tiền tối đa phải từ 1,000 VND")]
		public long MaxTopUpVnd { get; set; }

		/// <summary>Bắt buộc ghi lý do thay đổi — lưu audit trail.</summary>
		[Required(ErrorMessage = "Vui lòng ghi lý do thay đổi tỷ giá")]
		[MaxLength(500)]
		public string Note { get; set; } = string.Empty;

		public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
		{
			if (MaxTopUpVnd <= MinTopUpVnd)
				yield return new ValidationResult(
					"Số tiền tối đa phải lớn hơn số tiền tối thiểu.",
					[nameof(MaxTopUpVnd), nameof(MinTopUpVnd)]);
		}
	}
}
