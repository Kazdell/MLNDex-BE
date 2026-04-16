using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Application.Interfaces.Financial;
using Application.DTOs.Financial;  
using Application.Interfaces.Financial;

namespace mlndex_backend.Controllers.Payment
{
    [ApiController]
    [Route("api/withdrawal")]
    [Authorize]
    public class WithdrawalController : ControllerBase
    {
        private readonly IWithdrawalService _withdrawalService;

        public WithdrawalController(IWithdrawalService withdrawalService)
        {
            _withdrawalService = withdrawalService;
        }

        private int GetCurrentUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)
                     ?? User.FindFirst("sub")
                     ?? User.FindFirst("userId");
            return int.Parse(claim!.Value);
        }

        /// <summary>
        /// POST /api/withdrawal/request
        /// User/Creator tạo yêu cầu rút tiền
        /// </summary>
        [HttpPost("request")]
        public async Task<IActionResult> Request(
            [FromBody] CreateWithdrawalRequestDto dto,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            var result = await _withdrawalService.RequestAsync(userId, dto, cancellationToken);
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// GET /api/withdrawal/my?page=1&pageSize=20
        /// Lịch sử rút tiền của user hiện tại
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMy(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            var userId = GetCurrentUserId();
            var request = new WithdrawalReviewListRequest
            {
                UserId = userId,
                Page = page,
                PageSize = pageSize,
            };
            var result = await _withdrawalService.GetPendingAsync(request, cancellationToken);
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// GET /api/withdrawal/{id}
        /// Chi tiết một yêu cầu rút tiền
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken = default)
        {
            var result = await _withdrawalService.GetByIdAsync(id, cancellationToken);
            if (result is null) return NotFound();
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// GET /api/withdrawal/pending?page=1&pageSize=20
        /// [Admin] Danh sách yêu cầu chờ duyệt
        /// </summary>
        [HttpGet("pending")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetPending(
            [FromQuery] WithdrawalReviewListRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = await _withdrawalService.GetPendingAsync(request, cancellationToken);
            return Ok(new { success = true, data = result });
        }

        /// <summary>
        /// PUT /api/withdrawal/{id}/decide
        /// [Admin] Duyệt hoặc từ chối yêu cầu
        /// </summary>
        [HttpPut("{id:int}/decide")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Decide(
            int id,
            [FromBody] WithdrawalDecisionRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = await _withdrawalService.DecideAsync(id, request, cancellationToken);
            return Ok(new { success = true, data = result });
        }
    }
}
