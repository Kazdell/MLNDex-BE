using Application.DTOs.Common;
using Application.DTOs.Creator;
using Application.DTOs.Revenue.Request;
using Application.Exceptions;
using Application.Interfaces.Creator;
using Application.Interfaces.Revenue;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Creator
{
    [ApiController]
    [Route("api/creator")]
    [Authorize]
    public class CreatorController : BaseController
    {
        private readonly ICreatorService _creatorService;
        private readonly IRevenueService _revenueService;

        public CreatorController(ICreatorService creatorService, IRevenueService revenueService)
        {
            _creatorService = creatorService;
            _revenueService = revenueService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] CreatorRegisterDto dto, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return UnauthorizedResponse();

      try
      {
        var result = await _creatorService.RegisterAsync(userId, dto, ct);
        return OkResponse(result, "Đăng ký nhà sáng tạo thành công!");
      }
      catch (InvalidOperationException)
      {
        throw new AppException(ErrorCodes.INVALID_INPUT);
      }
    }

        // --- New Unlock Settings Endpoints ---

        /// <summary>
        /// Lấy thông tin cấu hình mở khóa nội dung của nhà sáng tạo.
        /// </summary>
        [Authorize(Roles = "CREATOR,ADMIN")]
        [HttpGet("settings/unlock-settings")]
        public async Task<IActionResult> GetUnlockSettings(CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return UnauthorizedResponse();

            var settings = await _creatorService.GetUnlockSettingsAsync(userId, ct);
            return OkResponse(settings, "Lấy cấu hình mở khóa thành công.");

        }

        /// <summary>
        /// Cập nhật cấu hình mở khóa nội dung.
        /// </summary>
        [Authorize(Roles = "CREATOR,ADMIN")]
        [HttpPut("settings/unlock-settings")]
        public async Task<IActionResult> UpdateUnlockSettings([FromBody] UpdateUnlockSettingsDto dto, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return UnauthorizedResponse();
            var success = await _creatorService.UpdateUnlockSettingsAsync(userId, dto, ct);

            if (!success)
            {
                throw new AppException(ErrorCodes.NOT_FOUND);
            }

            return OkResponse(true, "Cập nhật cấu hình mở khóa thành công!");
        }

        [Authorize(Roles = "CREATOR,ADMIN")]
        [HttpGet("revenue")]
        public async Task<IActionResult> GetCreatorRevenue([FromQuery] RevenueQueryDto query, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return UnauthorizedResponse();
            var result = await _revenueService.GetCreatorRevenueAsync(userId, query, ct);
            return OkResponse(result);
        }

        [Authorize(Roles = "CREATOR,ADMIN")]
        [HttpGet("revenue/series/{seriesId}")]
        public async Task<IActionResult> GetSeriesRevenue(int seriesId, [FromQuery] RevenueQueryDto query, CancellationToken ct)
        {
            var userId = GetUserId();
            if (userId == 0) return UnauthorizedResponse();
            var result = await _revenueService.GetSeriesRevenueAsync(userId, seriesId, query, ct);
            return OkResponse(result);
        }
    }
}
