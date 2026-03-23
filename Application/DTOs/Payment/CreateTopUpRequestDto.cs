using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Request;

/// <summary>
/// User nạp tiền — 2 cách:
///   1. Chọn gói có sẵn (PackageId != null)
///   2. Nhập số tiền tự do (CustomAmountVnd != null)
/// Phải có đúng 1 trong 2.
/// </summary>
public class CreateTopUpRequestDto : IValidatableObject
{
	/// <summary>ID gói coin Admin tạo sẵn. Null nếu nhập tự do.</summary>
	public int? PackageId { get; set; }

	/// <summary>Số tiền VND nhập tự do. Null nếu chọn gói.</summary>
	[Range(1, long.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0")]
	public long? CustomAmountVnd { get; set; }

	/// <summary>
	/// Phương thức thanh toán.
	/// Giá trị hợp lệ: "PAYOS" | "VNPAY" | "MOMO"
	/// </summary>
	[Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán")]
	public string PaymentMethod { get; set; } = string.Empty;

	/// <summary>
	/// URL redirect về sau khi thanh toán xong (cả success lẫn cancel).
	/// Bắt buộc với PAYOS. Không cần với BANK_TRANSFER.
	/// </summary>
	public string? ReturnUrl { get; set; }

	/// <summary>
	/// URL redirect về khi user huỷ thanh toán trên trang PayOS.
	/// Nếu không truyền thì dùng ReturnUrl làm CancelUrl.
	/// </summary>
	public string? CancelUrl { get; set; }

	/// <summary>IP của user — controller tự inject, client không truyền.</summary>
	public string IpAddress { get; set; } = string.Empty;

	public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
	{
		var hasPackage = PackageId.HasValue;
		var hasCustom = CustomAmountVnd.HasValue;

		if (!hasPackage && !hasCustom)
			yield return new ValidationResult(
				"Vui lòng chọn gói hoặc nhập số tiền muốn nạp.",
				[nameof(PackageId), nameof(CustomAmountVnd)]);

		if (hasPackage && hasCustom)
			yield return new ValidationResult(
				"Chỉ được chọn gói hoặc nhập số tiền, không được điền cả hai.",
				[nameof(PackageId), nameof(CustomAmountVnd)]);

		if (string.IsNullOrWhiteSpace(ReturnUrl))
			yield return new ValidationResult(
				"ReturnUrl là bắt buộc.",
				[nameof(ReturnUrl)]);
	}
}