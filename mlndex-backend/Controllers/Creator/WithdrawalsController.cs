using Application.DTOs.Financial;
using Application.Interfaces.Financial;
using mlndex_backend.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace mlndex_backend.Controllers.Creator
{
  [Authorize(Roles = "Creator")]
  [Route("api/creator/[controller]")]
  [ApiController]
  public class WithdrawalsController : BaseController
  {
    private readonly IWithdrawalService _withdrawalService;

    public WithdrawalsController(IWithdrawalService withdrawalService)
    {
      _withdrawalService = withdrawalService;
    }

    [HttpPost]
    public async Task<IActionResult> RequestWithdrawal([FromBody] CreateWithdrawalRequestDto dto)
    {
      var userId = GetUserId();
      if (userId == 0) return UnauthorizedResponse();

      var result = await _withdrawalService.RequestAsync(userId, dto);
      return OkResponse(result, "Thành công");
    }
  }
}
