using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.DTOs.Payment;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Payment;

public class MoMoGatewayService : IPaymentGatewayService
{
  public string GatewayName => "MOMO";

  private readonly HttpClient _httpClient;
  private readonly ILogger<MoMoGatewayService> _logger;
  private readonly IConfiguration _configuration;

  private const string PaymentUrl = "https://test-payment.momo.vn/v2/gateway/api/create";

  private string PartnerCode => _configuration["MoMo:PartnerCode"]
      ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
  private string AccessKey => _configuration["MoMo:AccessKey"]
      ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
  private string SecretKey => _configuration["MoMo:SecretKey"]
      ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
  private string IpnUrl => _configuration["MoMo:IpnUrl"]
      ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
  private string ReturnUrl => _configuration["MoMo:ReturnUrl"]
      ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

  public MoMoGatewayService(
      IHttpClientFactory httpClientFactory,
      IConfiguration configuration,
      ILogger<MoMoGatewayService> logger)
  {
    _logger = logger;
    _configuration = configuration;
    _httpClient = httpClientFactory.CreateClient("MoMo");
    _httpClient.DefaultRequestHeaders.Accept
        .Add(new MediaTypeWithQualityHeaderValue("application/json"));
  }

  public async Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request)
  {
    try
    {
      var orderId = request.TxnRef;
      var requestId = request.TxnRef;
      var amount = request.AmountVnd.ToString();
      var orderInfo = request.Description;
      var returnUrl = request.ReturnUrl ?? ReturnUrl;
      var requestType = "payWithMethod";
      var extraData = string.Empty;

      // Build raw signature string — MoMo sort theo thứ tự cố định
      var rawSignature = $"accessKey={AccessKey}" +
                         $"&amount={amount}" +
                         $"&extraData={extraData}" +
                         $"&ipnUrl={IpnUrl}" +
                         $"&orderId={orderId}" +
                         $"&orderInfo={orderInfo}" +
                         $"&partnerCode={PartnerCode}" +
                         $"&redirectUrl={returnUrl}" +
                         $"&requestId={requestId}" +
                         $"&requestType={requestType}";

      var signature = ComputeHmacSha256(rawSignature);

      var body = new
      {
        partnerCode = PartnerCode,
        requestId,
        amount,
        orderId,
        orderInfo,
        redirectUrl = returnUrl,
        ipnUrl = IpnUrl,
        requestType,
        extraData,
        lang = "vi",
        signature
      };

      var response = await _httpClient.PostAsync(PaymentUrl,
          new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

      var responseBody = await response.Content.ReadAsStringAsync();

      if (!response.IsSuccessStatusCode)
      {
        _logger.LogError("[MoMo] Tạo link thất bại. Body={Body}", responseBody);
        return GatewayCreateResult.Fail($"MoMo lỗi: {responseBody}");
      }

      using var doc = JsonDocument.Parse(responseBody);

      // resultCode 0 = thành công
      var resultCode = doc.RootElement.GetProperty("resultCode").GetInt32();
      if (resultCode != 0)
      {
        var message = doc.RootElement.GetProperty("message").GetString();
        return GatewayCreateResult.Fail($"MoMo từ chối: {message}");
      }

      var payUrl = doc.RootElement.GetProperty("payUrl").GetString()!;

      _logger.LogInformation("[MoMo] Tạo link thành công. OrderId={OrderId}", orderId);
      return GatewayCreateResult.Success(payUrl);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "[MoMo] CreatePaymentAsync thất bại.");
      return GatewayCreateResult.Fail(ex.Message);
    }
  }

  public Task<PaymentCallbackDto> ParseAndVerifyCallbackAsync(object rawPayload)
  {
    var dto = rawPayload as MoMoCallbackDto
        ?? throw new ArgumentException("Payload không phải MoMoCallbackDto.");

    var isValid = VerifySignature(dto);

    if (!isValid)
      _logger.LogWarning("[MoMo] Signature không hợp lệ. OrderId={OrderId}", dto.OrderId);

    // resultCode 0 = thành công
    var status = dto.ResultCode == 0 ? "PAID"
        : dto.ResultCode == 1006 ? "CANCELLED"  // 1006 = user huỷ
        : "FAILED";

    return Task.FromResult(new PaymentCallbackDto
    {
      TxnRef = dto.RequestId,   // RequestId = TxnRef khi tạo đơn
      Status = status,
      AmountPaid = dto.Amount,
      GatewayTransactionId = dto.TransId,
      Gateway = GatewayName,
      IsSignatureValid = isValid,
      TransactionTime = DateTime.UtcNow
    });
  }

  private bool VerifySignature(MoMoCallbackDto dto)
  {
    try
    {
      var rawSignature = $"accessKey={AccessKey}" +
                         $"&amount={dto.Amount}" +
                         $"&extraData={dto.ExtraData}" +
                         $"&message={dto.Message}" +
                         $"&orderId={dto.OrderId}" +
                         $"&orderInfo={dto.OrderInfo}" +
                         $"&orderType={dto.OrderType}" +
                         $"&partnerCode={dto.PartnerCode}" +
                         $"&payType={dto.PayType}" +
                         $"&requestId={dto.RequestId}" +
                         $"&responseTime={dto.ResponseTime}" +
                         $"&resultCode={dto.ResultCode}" +
                         $"&transId={dto.TransId}";

      var computed = ComputeHmacSha256(rawSignature);
      return string.Equals(computed, dto.Signature, StringComparison.OrdinalIgnoreCase);
    }
    catch
    {
      return false;
    }
  }

  private string ComputeHmacSha256(string data)
  {
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(SecretKey));
    return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))
        .ToLowerInvariant();
  }
}
