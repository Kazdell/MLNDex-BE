using Application.DTOs.Payment;
using Application.DTOs.Request;

namespace Application.Interfaces;

/// <summary>
/// Toàn bộ luồng nạp tiền — top-up, xem ví, xem lịch sử giao dịch.
/// </summary>
public interface ITopUpService
{
	/// <summary>
	/// Lấy tỉ lệ quy đổi hiện tại từ DB.
	/// Frontend gọi để preview coins realtime khi user nhập số tiền.
	/// </summary>
	Task<CoinRateResponseDto> GetCoinRateAsync();

	/// <summary>
	/// Lấy danh sách gói coin đang active.
	/// Hiển thị ở trang /wallet để user chọn.
	/// </summary>
	Task<List<CoinPackageResponseDto>> GetActivePackagesAsync();

	/// <summary>
	/// Lấy thông tin ví của user (số dư, tổng nạp, tổng tiêu).
	/// </summary>
	Task<WalletResponseDto> GetWalletAsync(int userId);

	/// <summary>
	/// Lấy lịch sử giao dịch của user, có phân trang.
	/// </summary>
	Task<TransactionPagedResponseDto> GetTransactionHistoryAsync(int userId, int page = 1, int pageSize = 20);

	/// <summary>
	/// Khởi tạo giao dịch nạp tiền.
	/// Validate input → tạo Transaction Pending → gọi PayOS hoặc Bank Transfer.
	/// Trả về CheckoutUrl (PayOS) hoặc BankInfo (Bank Transfer).
	/// </summary>
	Task<TopUpInitResponseDto> InitiateAsync(int userId, CreateTopUpRequestDto request);

	/// <summary>
	/// Xử lý webhook từ PayOS sau khi user thanh toán xong.
	/// Idempotent — gọi nhiều lần cùng TxnRef vẫn an toàn.
	/// PayOS gộp tất cả ngân hàng và ví điện tử vào 1 webhook duy nhất.
	/// </summary>
	Task<TopUpCallbackResponseDto> HandlePayOsWebhookAsync(PayOsWebhookData webhookData);

	/// <summary>
	/// Bước 2b: Xử lý IPN từ VNPay.
	/// </summary>
	Task<TopUpCallbackResponseDto> HandleVNPayCallbackAsync(VNPayCallbackDto dto);

	/// <summary>
	/// Bước 2c: Xử lý IPN từ MoMo.
	/// </summary>
	Task<TopUpCallbackResponseDto> HandleMoMoCallbackAsync(MoMoCallbackDto dto);
}