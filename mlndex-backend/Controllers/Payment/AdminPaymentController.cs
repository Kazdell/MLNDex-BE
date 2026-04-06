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
  //private readonly ICoinRateService _coinRateService;

  public AdminPaymentController(
      ICoinPackageService coinPackageService
      )
  {
    _coinPackageService = coinPackageService;
    //_coinRateService = coinRateService;
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
    return OkResponse<object?>(null!, "Vô hiệu hoá gói coin thành công.");
  }
}
