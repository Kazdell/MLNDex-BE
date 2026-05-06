using Application.DTOs.Common;
using Application.Exceptions;
using Application.DTOs.System;
using Application.Interfaces.System;
using Application.Interfaces.Moderation;
using Application.DTOs.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace mlndex_backend.Controllers.Admin
{
  [Route("api/admin/system-config")]
  [Authorize(Roles = "ADMIN")]
  public class SystemConfigController : BaseController
  {
    private readonly ISystemConfigService _service;
    private readonly IBlacklistProvider _blacklistProvider;

		public SystemConfigController(ISystemConfigService service, IBlacklistProvider blacklistProvider)
		{
			_service = service;
			_blacklistProvider = blacklistProvider;
		}

		[HttpGet]
		[AllowAnonymous]
		public async Task<IActionResult> Get(CancellationToken cancellationToken)
		{
			var config = await _service.GetAsync(cancellationToken);
			return OkResponse(config);
		}

    [HttpPut]
    public async Task<IActionResult> Update(
    [FromBody] SystemConfigDto dto,
    CancellationToken cancellationToken
)
    {
      if (!ModelState.IsValid)
        throw new AppException(ErrorCodes.INVALID_INPUT);
      var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
      var updated = await _service.UpdateAsync(dto, userId, cancellationToken);
      return OkResponse(updated, "Updated");
    }

    [HttpGet("rate/preview")]
    [AllowAnonymous]
    public async Task<IActionResult> PreviewCoins([FromQuery] long amountVnd)
    {
      var coins = await _service.CalculateCoinsAsync(amountVnd);
      return OkResponse(new { amountVnd, coinsWillReceive = coins });
    }

		[HttpGet("blacklist-file")]
		public async Task<IActionResult> GetBlacklistFile()
		{
			var json = await _blacklistProvider.GetBlacklistJsonAsync();
			return Content(json, "application/json");
		}

		[HttpPost("blacklist-file")]
		public async Task<IActionResult> AddBlacklistWord([FromBody] AddBlacklistWordRequest request)
		{
			await _blacklistProvider.AddBlacklistWordAsync(request.Word, request.Category, request.Severity);
			return OkResponse<object>(null, "Added successfully");
		}

		[HttpGet("thresholds")]
		public async Task<IActionResult> GetThresholds()
		{
			var thresholds = await _blacklistProvider.GetThresholdsAsync();
			return OkResponse(thresholds);
		}

		[HttpPut("thresholds")]
		public async Task<IActionResult> UpdateThresholds([FromBody] Dictionary<string, ThresholdRule> thresholds)
		{
			await _blacklistProvider.UpdateThresholdsAsync(thresholds);
			return OkResponse<object>(null, "Thresholds updated");
		}
  }
}
