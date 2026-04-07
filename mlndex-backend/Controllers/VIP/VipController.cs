using Application.DTOs.VIP;
using Application.Interfaces.VIP;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;

[ApiController]
[Route("api/vip")]
[Authorize]
public class VipController : BaseController
{
	private readonly IVipService _vipService;

	public VipController(IVipService vipService)
	{
		_vipService = vipService;
	}

	[AllowAnonymous]
	[HttpGet("plans")]
	public async Task<IActionResult> GetPlans()
	{
		var result = await _vipService.GetActivePlansAsync();
		return OkResponse(result);
	}

	[HttpGet("status")]
	public async Task<IActionResult> GetStatus()
	{
		var isVip = await _vipService.IsUserVipAsync(GetUserId());
		return OkResponse(new { isVip });
	}

	[HttpGet("subscription")]
	public async Task<IActionResult> GetActiveSubscription()
	{
		var result = await _vipService.GetActiveSubscriptionAsync(GetUserId());
		return OkResponse(result);
	}

	[HttpGet("subscription/history")]
	public async Task<IActionResult> GetHistory()
	{
		var result = await _vipService.GetSubscriptionHistoryAsync(GetUserId());
		return OkResponse(result);
	}

	[HttpPost("purchase")]
	public async Task<IActionResult> Purchase([FromBody] PurchaseVipRequestDto request)
	{
		try
		{
			var result = await _vipService.PurchaseVipAsync(GetUserId(), request);
			return OkResponse(result, "Mua VIP thành công.");
		}
		catch (InvalidOperationException ex)
		{
			return BadRequestResponse(ex.Message);
		}
	}

	[HttpPost("subscription/{id}/cancel")]
	public async Task<IActionResult> Cancel(int id)
	{
		try
		{
			var result = await _vipService.CancelSubscriptionAsync(GetUserId(), id);
			return OkResponse(result, "Huỷ subscription thành công.");
		}
		catch (InvalidOperationException ex)
		{
			return BadRequestResponse(ex.Message);
		}
	}

	[AllowAnonymous]
	[HttpGet("chapters/{id:int}/access")]
	public async Task<IActionResult> CheckAccess(int id, CancellationToken cancellationToken)
	{
		int? userId = GetUserId() == 0 ? null : GetUserId();

		try
		{
			var canRead = await _vipService.CanUserReadChapterAsync(id, userId, cancellationToken);
			return OkResponse(new { canRead });
		}
		catch (Exception ex)
		{
			return ErrorResponse(ex.Message);
		}
	}
}