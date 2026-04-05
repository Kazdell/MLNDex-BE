using System.Net;
using System.Security.Cryptography;
using System.Text;
using Application.DTOs.Payment;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Payment;

public class VNPayGatewayService : IPaymentGatewayService
{
  public string GatewayName => "VNPAY";

  private readonly ILogger<VNPayGatewayService> _logger;
  private readonly IConfiguration _configuration;

  private string TmnCode => _configuration["VNPay:TmnCode"]
      ?? throw new InvalidOperationException("Thiếu VNPay:TmnCode");
  private string HashSecret => _configuration["VNPay:HashSecret"]
      ?? throw new InvalidOperationException("Thiếu VNPay:HashSecret");
  private string PaymentUrl => _configuration["VNPay:PaymentUrl"]
      ?? "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";
  private string ReturnUrl => _configuration["VNPay:ReturnUrl"]
      ?? throw new InvalidOperationException("Thiếu VNPay:ReturnUrl");

  public VNPayGatewayService(IConfiguration configuration, ILogger<VNPayGatewayService> logger)
  {
    _logger = logger;
    _configuration = configuration;
  }

  public Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request)
  {
    try
    {
      var now = DateTime.UtcNow.AddHours(7); // VNPay dùng giờ Việt Nam

      // Build params theo đúng thứ tự alphabet — VNPay yêu cầu
      var params_ = new SortedDictionary<string, string>
      {
        ["vnp_Version"] = "2.1.0",
        ["vnp_Command"] = "pay",
        ["vnp_TmnCode"] = TmnCode,
        ["vnp_Amount"] = (request.AmountVnd * 100).ToString(), // VNPay tính theo đơn vị x100
        ["vnp_CreateDate"] = now.ToString("yyyyMMddHHmmss"),
        ["vnp_CurrCode"] = "VND",
        ["vnp_IpAddr"] = request.IpAddress,
        ["vnp_Locale"] = "vn",
        ["vnp_OrderInfo"] = request.Description,
        ["vnp_OrderType"] = "other",
        ["vnp_ReturnUrl"] = request.ReturnUrl ?? ReturnUrl,
        ["vnp_TxnRef"] = request.TxnRef,
        ["vnp_ExpireDate"] = now.AddMinutes(15).ToString("yyyyMMddHHmmss"),
      };

      var queryString = string.Join("&",
          params_.Select(p => $"{p.Key}={WebUtility.UrlEncode(p.Value)}"));

      var signature = ComputeHmacSha512(queryString);
      var paymentUrl = $"{PaymentUrl}?{queryString}&vnp_SecureHash={signature}";

      _logger.LogInformation("[VNPay] Tạo link thành công. TxnRef={TxnRef}", request.TxnRef);
      return Task.FromResult(GatewayCreateResult.Success(paymentUrl));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "[VNPay] CreatePaymentAsync thất bại.");
      return Task.FromResult(GatewayCreateResult.Fail(ex.Message));
    }
  }

  public Task<PaymentCallbackDto> ParseAndVerifyCallbackAsync(object rawPayload)
  {
    var dto = rawPayload as VNPayCallbackDto
        ?? throw new ArgumentException("Payload không phải VNPayCallbackDto.");

    // Verify signature — build lại query string không có vnp_SecureHash
    var isValid = VerifySignature(dto);

    if (!isValid)
      _logger.LogWarning("[VNPay] Signature không hợp lệ. TxnRef={TxnRef}", dto.vnp_TxnRef);

    // vnp_ResponseCode "00" = thành công
    var status = dto.vnp_ResponseCode == "00" && dto.vnp_TransactionStatus == "00"
        ? "PAID"
        : dto.vnp_ResponseCode == "24" ? "CANCELLED"  // 24 = user huỷ
        : "FAILED";

    // VNPay trả amount * 100
    var amountPaid = long.TryParse(dto.vnp_Amount, out var amt) ? amt / 100 : 0;

    return Task.FromResult(new PaymentCallbackDto
    {
      TxnRef = dto.vnp_TxnRef,
      Status = status,
      AmountPaid = amountPaid,
      GatewayTransactionId = dto.vnp_TransactionNo,
      Gateway = GatewayName,
      IsSignatureValid = isValid,
      TransactionTime = DateTime.UtcNow
    });
  }

  private bool VerifySignature(VNPayCallbackDto dto)
  {
    try
    {
      var params_ = new SortedDictionary<string, string>
      {
        ["vnp_Amount"] = dto.vnp_Amount,
        ["vnp_BankCode"] = dto.vnp_BankCode,
        ["vnp_BankTranNo"] = dto.vnp_BankTranNo,
        ["vnp_CardType"] = dto.vnp_CardType,
        ["vnp_OrderInfo"] = dto.vnp_OrderInfo,
        ["vnp_PayDate"] = dto.vnp_PayDate,
        ["vnp_ResponseCode"] = dto.vnp_ResponseCode,
        ["vnp_TmnCode"] = dto.vnp_TmnCode,
        ["vnp_TransactionNo"] = dto.vnp_TransactionNo,
        ["vnp_TransactionStatus"] = dto.vnp_TransactionStatus,
        ["vnp_TxnRef"] = dto.vnp_TxnRef,
      };

      // Bỏ các field rỗng
      var filtered = params_
          .Where(p => !string.IsNullOrEmpty(p.Value))
          .ToDictionary(p => p.Key, p => p.Value);

      var queryString = string.Join("&",
          filtered.Select(p => $"{p.Key}={WebUtility.UrlEncode(p.Value)}"));

      var computed = ComputeHmacSha512(queryString);
      return string.Equals(computed, dto.vnp_SecureHash, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }

  private string ComputeHmacSha512(string data)
  {
    using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(HashSecret));
    return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))
        .ToLowerInvariant();
  }
}
