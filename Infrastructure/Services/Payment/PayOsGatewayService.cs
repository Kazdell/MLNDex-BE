using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.DTOs.Payment;
using Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services.Payment;

public class PayOsGatewayService : IPaymentGatewayService
{
	public string GatewayName => "PAYOS";

	private readonly HttpClient _httpClient;
	private readonly ILogger<PayOsGatewayService> _logger;
	private readonly IConfiguration _configuration;

	private const string BaseUrl = "https://api-merchant.payos.vn";

	private string ClientId => _configuration["PayOS:ClientId"]
		?? throw new InvalidOperationException("Thiếu PayOS:ClientId");
	private string ApiKey => _configuration["PayOS:ApiKey"]
		?? throw new InvalidOperationException("Thiếu PayOS:ApiKey");
	private string ChecksumKey => _configuration["PayOS:ChecksumKey"]
		?? throw new InvalidOperationException("Thiếu PayOS:ChecksumKey");

	public PayOsGatewayService(
		IHttpClientFactory httpClientFactory,
		IConfiguration configuration,
		ILogger<PayOsGatewayService> logger)
	{
		_logger = logger;
		_configuration = configuration;
		_httpClient = httpClientFactory.CreateClient("PayOS");
		_httpClient.BaseAddress = new Uri(BaseUrl);
		_httpClient.DefaultRequestHeaders.Accept
			.Add(new MediaTypeWithQualityHeaderValue("application/json"));
	}

	public async Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request)
	{
		try
		{
			if (!long.TryParse(request.TxnRef, out var orderCode))
				return GatewayCreateResult.Fail($"TxnRef '{request.TxnRef}' không hợp lệ.");

			var safeDesc = request.Description.Length > 25
				? request.Description[..25]
				: request.Description;

			var amount = (int)request.AmountVnd;

			_httpClient.DefaultRequestHeaders.Remove("x-client-id");
			_httpClient.DefaultRequestHeaders.Remove("x-api-key");
			_httpClient.DefaultRequestHeaders.Add("x-client-id", ClientId);
			_httpClient.DefaultRequestHeaders.Add("x-api-key", ApiKey);

			var body = new
			{
				orderCode,
				amount,
				description = safeDesc,
				cancelUrl = request.CancelUrl,
				returnUrl = request.ReturnUrl,
				expiredAt = (int)DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds(),
				items = new[] { new { name = "Nap Coins", quantity = 1, price = amount } },
				signature = BuildSignature(orderCode, amount, safeDesc,
					request.CancelUrl!, request.ReturnUrl!)
			};

			var response = await _httpClient.PostAsync("/v2/payment-requests",
				new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));

			var responseBody = await response.Content.ReadAsStringAsync();

			if (!response.IsSuccessStatusCode)
			{
				_logger.LogError("[PayOS] Tạo link thất bại. Body={Body}", responseBody);
				return GatewayCreateResult.Fail($"PayOS lỗi: {responseBody}");
			}

			using var doc = JsonDocument.Parse(responseBody);
			var checkoutUrl = doc.RootElement
				.GetProperty("data")
				.GetProperty("checkoutUrl")
				.GetString()!;

			_logger.LogInformation("[PayOS] Tạo link thành công. OrderCode={OrderCode}", orderCode);
			return GatewayCreateResult.Success(checkoutUrl);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "[PayOS] CreatePaymentAsync thất bại.");
			return GatewayCreateResult.Fail(ex.Message);
		}
	}

	public Task<PaymentCallbackDto> ParseAndVerifyCallbackAsync(object rawPayload)
	{
		var webhookData = rawPayload as PayOsWebhookData
			?? throw new ArgumentException("Payload không phải PayOsWebhookData.");

		var computedSig = BuildWebhookSignature(webhookData);

		// Nếu không có signature (return URL) thì tin vào Success flag
		var isValid = string.IsNullOrEmpty(webhookData.Signature)
			? true
			: string.Equals(computedSig, webhookData.Signature, StringComparison.OrdinalIgnoreCase);

		if (!isValid)
			_logger.LogWarning("[PayOS] Webhook signature không hợp lệ. OrderCode={OrderCode}",
				webhookData.OrderCode);

		var status = webhookData.Success && webhookData.Code == "00" ? "PAID"
			: webhookData.Code == "01" ? "CANCELLED"
			: "FAILED";

		var callback = new PaymentCallbackDto
		{
			TxnRef = webhookData.OrderCode.ToString(),
			Status = status,
			AmountPaid = webhookData.Amount,
			GatewayTransactionId = webhookData.Reference,
			Gateway = GatewayName,
			IsSignatureValid = isValid,
			TransactionTime = DateTime.UtcNow
		};

		_logger.LogInformation(
			"[PayOS] ParseCallback result. TxnRef={TxnRef} Status={Status} IsValid={IsValid}",
			callback.TxnRef, callback.Status, callback.IsSignatureValid);

		return Task.FromResult(callback);
	}	

	private string BuildSignature(long orderCode, int amount, string description,
		string cancelUrl, string returnUrl)
	{
		var data = $"amount={amount}&cancelUrl={cancelUrl}&description={description}" +
				   $"&orderCode={orderCode}&returnUrl={returnUrl}";
		return ComputeHmacSha256(data);
	}

	private string BuildWebhookSignature(PayOsWebhookData data)
	{
		var sorted = new SortedDictionary<string, string>
		{
			["accountNumber"] = data.AccountNumber,
			["amount"] = data.Amount.ToString(),
			["description"] = data.Description,
			["orderCode"] = data.OrderCode.ToString(),
			["paymentLinkId"] = data.PaymentLinkId,
			["reference"] = data.Reference,
			["transactionDateTime"] = data.TransactionDateTime,
			["virtualAccountName"] = data.VirtualAccountName,
			["virtualAccountNumber"] = data.VirtualAccountNumber,
		};
		return ComputeHmacSha256(string.Join("&", sorted.Select(p => $"{p.Key}={p.Value}")));
	}

	private string ComputeHmacSha256(string data)
	{
		using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChecksumKey));
		return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)))
			.ToLowerInvariant();
	}
}