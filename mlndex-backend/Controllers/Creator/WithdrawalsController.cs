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
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
            {
                return Unauthorized();
            }

            try
            {
                var result = await _withdrawalService.RequestAsync(userId, dto);
                return Ok(new { success = true, data = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { success = false, message = "Đã có lỗi xảy ra khi xử lý yêu cầu." });
            }
        }
    }
}
