using Application.DTOs.System;
using Application.Interfaces.System;
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

		public SystemConfigController(ISystemConfigService service)
		{
			_service = service;
		}

		[HttpGet]
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
        return BadRequestResponse("Invalid payload");
      try
      {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var updated = await _service.UpdateAsync(dto, userId, cancellationToken);
        return OkResponse(updated, "Updated");
      }
      catch (ArgumentException ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    /// <summary>Preview coins sẽ nhận khi nhập số VND.</summary>
    [HttpGet("rate/preview")]
    public async Task<IActionResult> PreviewCoins([FromQuery] long amountVnd)
    {
      var coins = await _service.CalculateCoinsAsync(amountVnd);
      return OkResponse(new { amountVnd, coinsWillReceive = coins });
    }
  }
}
