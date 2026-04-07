using Application.DTOs.VIP;
using Application.Interfaces.VIP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;

[ApiController]
[Route("api/admin/vip")]
[Authorize(Roles = "ADMIN")]
public class AdminVipController : BaseController
{
	private readonly IVipService _vipService;

	public AdminVipController(IVipService vipService)
	{
		_vipService = vipService;
	}

	[HttpGet("plans")]
	public async Task<IActionResult> GetAllPlans()
	{
		var result = await _vipService.GetAllPlansAsync();
		return OkResponse(result);
	}

	[HttpPost("plans")]
	public async Task<IActionResult> CreatePlan([FromBody] CreateVipPlanDto request)
	{
		var result = await _vipService.CreatePlanAsync(request);
		return OkResponse(result, "Tạo gói VIP thành công.");
	}

	[HttpPut("plans/{id}")]
	public async Task<IActionResult> UpdatePlan(
		int id, [FromBody] UpdateVipPlanDto request)
	{
		var result = await _vipService.UpdatePlanAsync(id, request);
		return OkResponse(result, "Cập nhật gói VIP thành công.");
	}

	[HttpDelete("plans/{id}")]
	public async Task<IActionResult> DeletePlan(int id)
	{
		await _vipService.DeletePlanAsync(id);
		return OkResponse<object>(null!, "Xoá gói VIP thành công.");
	}
}