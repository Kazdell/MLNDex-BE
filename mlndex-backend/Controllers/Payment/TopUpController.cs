using Application.DTOs.Common;
using Application.Exceptions;
using Application.DTOs.Payment;
using Application.DTOs.Request;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace mlndex_backend.Controllers.Payment;

[Route("api/topup")]
public class TopUpController : BaseController
{
  private readonly ITopUpService _topUpService;
  private readonly ILogger<TopUpController> _logger;

  public TopUpController(ITopUpService topUpService, ILogger<TopUpController> logger)
  {
    _topUpService = topUpService;
    _logger = logger;
  }

  // ────────────────────────────────────────────────
  // Queries
  // ────────────────────────────────────────────────

  /// <summary>Danh sách gói coin đang active.</summary>
  [HttpGet("packages")]
  public async Task<IActionResult> GetPackages()
  {
    var packages = await _topUpService.GetActivePackagesAsync();
    return OkResponse(packages);
  }

  /// <summary>Số dư ví của user đang đăng nhập.</summary>
  [HttpGet("wallet")]
  [Authorize]
  public async Task<IActionResult> GetWallet()
  {
    var userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse();
    var wallet = await _topUpService.GetWalletAsync(userId);
    return OkResponse(wallet);
  }

  /// <summary>Lịch sử giao dịch — có phân trang.</summary>
  [HttpGet("transactions")]
  [Authorize]
  public async Task<IActionResult> GetTransactions(
      [FromQuery] int page = 1,
      [FromQuery] int pageSize = 20)
  {
    var userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse();

    var result = await _topUpService.GetTransactionHistoryAsync(userId, page, pageSize);
    return OkResponse(result);
  }

  // ────────────────────────────────────────────────
  // Initiate
  // ────────────────────────────────────────────────

  /// <summary>
  /// Tạo lệnh nạp tiền.
  /// PaymentMethod: "PAYOS" | "VNPAY" | "MOMO" | "BANK_TRANSFER"
  /// Trả về PaymentUrl hoặc BankInfo tuỳ method.
  /// </summary>
  [HttpPost("initiate")]
  [Authorize]
  public async Task<IActionResult> Initiate([FromBody] CreateTopUpRequestDto request)
  {
    if (!ModelState.IsValid)
      throw new AppException(ErrorCodes.INVALID_INPUT);

    var userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse();

    request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

    var result = await _topUpService.InitiateAsync(userId, request);
    return OkResponse(result, "Khởi tạo giao dịch thành công.");
  }

  // ────────────────────────────────────────────────
  // Callbacks — KHÔNG có [Authorize], cổng gọi server-to-server
  // ────────────────────────────────────────────────

  /// <summary>
  /// Webhook từ payOS.
  /// Nhận JSON payload, verify signature trong Service.
  /// Luôn trả 200 để payOS không retry.
  /// </summary>
  [HttpPost("callback/payos")]
  public async Task<IActionResult> PayOsCallback([FromBody] JsonElement payload)
  {
    try
    {
      var webhookData = MapPayOsWebhook(payload);
      var result = await _topUpService.HandlePayOsWebhookAsync(webhookData);
      return OkResponse(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "[PayOS Webhook] Xử lý thất bại.");
      return OkResponse(false);
    }
  }

  /// <summary>
  /// Return URL từ payOS sau khi user thanh toán — GET với query params.
  /// </summary>
  [HttpGet("callback/payos")]
  public async Task<IActionResult> PayOsReturn(
  [FromQuery(Name = "code")] string code,
  [FromQuery(Name = "id")] string paymentId,
  [FromQuery(Name = "cancel")] bool cancel,
  [FromQuery(Name = "status")] string paymentStatus,
  [FromQuery(Name = "orderCode")] long orderCode)
  {
    try
    {
      var webhookData = new PayOsWebhookData
      {
        Code = code,
        Success = !cancel && code == "00",
        OrderCode = orderCode,
        PaymentLinkId = paymentId,
        Signature = string.Empty
      };

      var result = await _topUpService.HandlePayOsWebhookAsync(webhookData);
      return OkResponse(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "[PayOS Return] Xử lý thất bại.");
      return OkResponse(false);
    }
  }

  /// <summary>
  /// IPN + Return URL từ VNPay — nhận GET request với query params.
  /// VNPay gọi cả IPN (server-to-server) và Return (redirect user về).
  /// </summary>
  [HttpGet("callback/vnpay")]
  public async Task<IActionResult> VNPayCallback([FromQuery] VNPayCallbackDto dto)
  {
    try
    {
      var result = await _topUpService.HandleVNPayCallbackAsync(dto);

      // Nếu là redirect từ browser (có vnp_TransactionStatus)
      // trả về 200 JSON — frontend tự xử lý redirect
      return OkResponse(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "[VNPay Callback] Xử lý thất bại.");
      return OkResponse(false);
    }
  }

  /// <summary>
  /// IPN từ MoMo — nhận POST JSON.
  /// Luôn trả 200 để MoMo không retry.
  /// </summary>
  [HttpPost("callback/momo")]
  public async Task<IActionResult> MoMoCallback([FromBody] MoMoCallbackDto dto)
  {
    try
    {
      var result = await _topUpService.HandleMoMoCallbackAsync(dto);
      return OkResponse(result);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "[MoMo Callback] Xử lý thất bại.");
      return OkResponse(false);
    }
  }

  // ────────────────────────────────────────────────
  // Private helpers
  // ────────────────────────────────────────────────

  /// <summary>
  /// Parse JsonElement từ payOS webhook thành PayOsWebhookData.
  /// Tách ra helper để PayOsCallback gọn hơn.
  /// </summary>
  private static PayOsWebhookData MapPayOsWebhook(JsonElement payload)
  {
    var webhookData = new PayOsWebhookData
    {
      Code = GetString(payload, "code"),
      Desc = GetString(payload, "desc"),
      Success = payload.TryGetProperty("success", out var s) && s.GetBoolean(),
      Signature = GetString(payload, "signature"),
    };

    if (!payload.TryGetProperty("data", out var data))
      return webhookData;

    webhookData.OrderCode = data.TryGetProperty("orderCode", out var oc)
        ? oc.GetInt64() : 0;
    webhookData.Amount = data.TryGetProperty("amount", out var amt)
        ? amt.GetInt32() : 0;
    webhookData.Description = GetString(data, "description");
    webhookData.AccountNumber = GetString(data, "accountNumber");
    webhookData.Reference = GetString(data, "reference");
    webhookData.TransactionDateTime = GetString(data, "transactionDateTime");
    webhookData.Currency = GetString(data, "currency");
    webhookData.PaymentLinkId = GetString(data, "paymentLinkId");
    webhookData.VirtualAccountName = GetString(data, "virtualAccountName");
    webhookData.VirtualAccountNumber = GetString(data, "virtualAccountNumber");
    webhookData.CounterAccountBankId = GetString(data, "counterAccountBankId");
    webhookData.CounterAccountBankName = GetString(data, "counterAccountBankName");
    webhookData.CounterAccountName = GetString(data, "counterAccountName");
    webhookData.CounterAccountNumber = GetString(data, "counterAccountNumber");

    return webhookData;
  }

  private static string GetString(JsonElement element, string key) =>
      element.TryGetProperty(key, out var prop)
          ? prop.GetString() ?? string.Empty
          : string.Empty;
}
