using Application.DTOs.Payment;
using Application.Interfaces.Financial;
using Application.Interfaces.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Payment;
[Route("api/admin/payment")]
[Authorize(Roles = "Admin, ADMIN")]
public class AdminPaymentController : BaseController
{
	private readonly ICoinPackageService _coinPackageService;
	private readonly ICoinRateService _coinRateService;

	public AdminPaymentController(
		ICoinPackageService coinPackageService,
		ICoinRateService coinRateService)
	{
		_coinPackageService = coinPackageService;
		_coinRateService = coinRateService;
	}

	// ════════════════════════════════════════════════════════
	// COIN PACKAGE
	// ════════════════════════════════════════════════════════

	/// <summary>Lấy tất cả gói coin kể cả inactive.</summary>
	[HttpGet("packages")]
	public async Task<IActionResult> GetPackages()
	{
		var packages = await _coinPackageService.GetAllAsync(activeOnly: false);
		return OkResponse(packages);
	}

	/// <summary>Lấy chi tiết 1 gói.</summary>
	[HttpGet("packages/{id}")]
	public async Task<IActionResult> GetPackage(int id)
	{
		var package = await _coinPackageService.GetByIdAsync(id);
		if (package is null)
			return NotFoundResponse($"Không tìm thấy gói coin ID={id}.");
		return OkResponse(package);
	}

	/// <summary>Tạo gói coin mới.</summary>
	[HttpPost("packages")]
	public async Task<IActionResult> CreatePackage([FromBody] CreateCoinPackageDto dto)
	{
		if (!ModelState.IsValid)
			return BadRequestResponse("Dữ liệu không hợp lệ.");

		var result = await _coinPackageService.CreateAsync(dto);
		return OkResponse(result, "Tạo gói coin thành công.");
	}

	/// <summary>Cập nhật gói coin — chỉ truyền fields cần thay đổi.</summary>
	[HttpPut("packages/updates/{id}")]
	public async Task<IActionResult> UpdatePackage(int id, [FromBody] UpdateCoinPackageDto dto)
	{
		Console.WriteLine($"=== UpdatePackage called. id={id}, dto={System.Text.Json.JsonSerializer.Serialize(dto)}");
		if (!ModelState.IsValid)
			return BadRequestResponse("Dữ liệu không hợp lệ.");

		var result = await _coinPackageService.UpdateAsync(id, dto);
		return OkResponse(result, "Cập nhật gói coin thành công.");
	}

	/// <summary>Vô hiệu hoá gói coin (soft delete).</summary>
	[HttpDelete("packages/{id}")]
	public async Task<IActionResult> DeactivatePackage(int id)
	{
		await _coinPackageService.DeactivateAsync(id);
		return OkResponse<object>(null!, "Vô hiệu hoá gói coin thành công.");
	}

	// ════════════════════════════════════════════════════════
	// COIN RATE
	// ════════════════════════════════════════════════════════

	/// <summary>Xem tỷ giá đang active.</summary>
	[HttpGet("rate")]
	public async Task<IActionResult> GetActiveRate()
	{
		var rate = await _coinRateService.GetActiveRateAsync();
		return OkResponse(rate);
	}

	/// <summary>Lịch sử thay đổi tỷ giá — mới nhất lên đầu.</summary>
	[HttpGet("rate/history")]
	public async Task<IActionResult> GetRateHistory()
	{
		var history = await _coinRateService.GetHistoryAsync();
		return OkResponse(history);
	}

	/// <summary>
	/// Cập nhật tỷ giá mới.
	/// Tự động deactivate rate cũ, insert rate mới trong 1 transaction.
	/// Note bắt buộc — ghi lý do thay đổi.
	/// </summary>
	[HttpPost("rate")]
	public async Task<IActionResult> UpdateRate([FromBody] UpdateCoinRateDto dto)
	{
		if (!ModelState.IsValid)
			return BadRequestResponse("Dữ liệu không hợp lệ.");

		var adminId = GetUserId();
		if (adminId == 0) return UnauthorizedResponse();

		var result = await _coinRateService.UpdateRateAsync(adminId, dto);
		return OkResponse(result, "Cập nhật tỷ giá thành công.");
	}

	/// <summary>Preview coins sẽ nhận khi nhập số VND.</summary>
	[HttpGet("rate/preview")]
	public async Task<IActionResult> PreviewCoins([FromQuery] long amountVnd)
	{
		var coins = await _coinRateService.CalculateCoinsAsync(amountVnd);
		return OkResponse(new { amountVnd, coinsWillReceive = coins });
	}
}