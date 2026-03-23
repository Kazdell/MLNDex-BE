using Application.DTOs.Payment;
using Application.DTOs.Request;

namespace Application.Interfaces;

// ════════════════════════════════════════════════════════
// GATEWAY ABSTRACTION
// ════════════════════════════════════════════════════════

/// <summary>
/// Contract chung cho mọi cổng thanh toán.
/// Mỗi cổng implement 1 class riêng:
///   - PayOsGatewayService
///   - VNPayGatewayService
///   - MoMoGatewayService
/// TopUpService không biết cổng cụ thể nào — chỉ gọi qua interface này.
/// </summary>
public interface IPaymentGatewayService
{
	/// <summary>"PAYOS" | "VNPAY" | "MOMO" | "BANK_TRANSFER"</summary>
	string GatewayName { get; }

	/// <summary>
	/// Tạo link thanh toán.
	/// Trả về URL để redirect user sang trang thanh toán của cổng.
	/// </summary>
	Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request);

	/// <summary>
	/// Verify chữ ký và parse raw callback/IPN thành PaymentCallbackDto chuẩn.
	/// Throw nếu signature không hợp lệ.
	/// </summary>
	Task<PaymentCallbackDto> ParseAndVerifyCallbackAsync(object rawPayload);
}

/// <summary>
/// Resolve đúng IPaymentGatewayService theo tên cổng.
/// Đăng ký tất cả implementations trong DI,
/// factory tự resolve theo GatewayName.
/// </summary>
public interface IPaymentGatewayFactory
{
	IPaymentGatewayService GetGateway(string gatewayName);
	IReadOnlyList<string> AvailableGateways { get; }
}

// ── Request / Result models ──

public class GatewayCreateRequest
{
	public string TxnRef { get; set; } = string.Empty;
	public long AmountVnd { get; set; }
	public string Description { get; set; } = string.Empty;
	public string? ReturnUrl { get; set; }
	public string? CancelUrl { get; set; }
	public string BuyerName { get; set; } = string.Empty;
	public string BuyerEmail { get; set; } = string.Empty;
	public string IpAddress { get; set; } = string.Empty;
}

public class GatewayCreateResult
{
	public bool IsSuccess { get; set; }
	public string? PaymentUrl { get; set; }
	public string? GatewayOrderId { get; set; }
	public string? ErrorMessage { get; set; }

	public static GatewayCreateResult Success(string paymentUrl, string? gatewayOrderId = null)
		=> new() { IsSuccess = true, PaymentUrl = paymentUrl, GatewayOrderId = gatewayOrderId };

	public static GatewayCreateResult Fail(string error)
		=> new() { IsSuccess = false, ErrorMessage = error };
}