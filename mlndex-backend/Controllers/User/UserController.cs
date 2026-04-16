using Application.Exceptions;
using Application.DTOs.User;
using Application.DTOs.Common;
using Application.Interfaces.User;
using Application.Interfaces.Creator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;

namespace mlndex_backend.Controllers.User
{
  [ApiController]
  [Route("api/user")]
  [Authorize]
  public class UserController : BaseController
  {
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
      _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
      var userId = GetUserId();
      if (userId < 0) return UnauthorizedResponse();
      var profile = await _userService.GetProfileAsync(userId, cancellationToken);

      if (profile == null) throw new AppException(ErrorCodes.USER_NOT_FOUND);

      return OkResponse(profile);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto, CancellationToken cancellationToken)
    {
      var userId = GetUserId();
      if (userId < 0) return UnauthorizedResponse();
      var result = await _userService.UpdateProfileAsync(userId, dto, cancellationToken);
      return result ? Ok(new ApiResponse<string>(true, "Profile updated successfully")) : BadRequest();
    }

    [HttpGet("reading-history")]
    public async Task<IActionResult> GetReadingHistory(CancellationToken cancellationToken)
    {
      var userId = GetUserId();
      if (userId < 0) return UnauthorizedResponse();
      var history = await _userService.GetReadingHistoryAsync(userId, cancellationToken);
      return Ok(new ApiResponse<List<ReadingHistoryDto>>(true, "Lấy lịch sử đọc thành công", history));
    }

    [HttpGet("stats")]
    [Authorize(Roles = "MODERATOR,ADMIN")] // Only admins/mods should see this
    public async Task<IActionResult> GetUserStats(
        [FromQuery] int days = 7,
        CancellationToken cancellationToken = default)
    {
      var stats = await _userService.GetUserStatsAsync(days, cancellationToken);
      return OkResponse(stats);
    }

    [HttpGet("membership/plans")]
    [AllowAnonymous] // Cho phép khách xem các gói cước
    public async Task<IActionResult> GetVipPlans(CancellationToken cancellationToken)
    {
      var plans = await _userService.GetVipPlansAsync(cancellationToken);
      return Ok(new ApiResponse<List<VipPlanDto>>(true, "Lấy danh sách gói VIP thành công", plans));
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 15,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
      var result = await _userService.SearchUsersAsync(q ?? "", page, pageSize, role, status, cancellationToken);
      return OkResponse(result);
    }

    [HttpGet("profile/{username}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProfile(string username, CancellationToken cancellationToken)
    {
      var profile = await _userService.GetPublicProfileAsync(username, cancellationToken);
      if (profile == null) throw new AppException(ErrorCodes.USER_NOT_FOUND);
      return OkResponse(profile);
    }
    [HttpGet("settings")]
    public async Task<IActionResult> GetUserSettings(CancellationToken cancellationToken)
    {
      var userId = GetUserId();
      if (userId < 0) return UnauthorizedResponse();
      var settings = await _userService.GetUserSettingsAsync(userId, cancellationToken);
      if (settings == null) throw new AppException(ErrorCodes.USER_NOT_FOUND);
      return OkResponse(settings);
    }

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateUserSettings([FromBody] UserSettingsDto dto, CancellationToken cancellationToken)
    {
      var userId = GetUserId();
      if (userId < 0) return UnauthorizedResponse();
      var result = await _userService.UpdateUserSettingsAsync(userId, dto, cancellationToken);
      return result ? Ok(new ApiResponse<string>(true, "Cập nhật cài đặt thành công")) : BadRequest();
    }

    [HttpPost("profile/avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file, [FromServices] IStorageService storageService, CancellationToken cancellationToken)
    {
      var userId = GetUserId();
      if (userId < 0) return UnauthorizedResponse();

      if (file == null || file.Length == 0) throw new AppException(ErrorCodes.INVALID_INPUT);

      using var stream = file.OpenReadStream();
      var folder = $"users/{userId}/avatar";
      var url = await storageService.UploadAsync(stream, file.FileName, folder, cancellationToken);

      await _userService.UpdateProfileAsync(userId, new UpdateProfileDto { Avatar = url }, cancellationToken);

      return OkResponse(url);
    }

    [HttpPost("profile/banner")]
    public async Task<IActionResult> UploadBanner(IFormFile file, [FromServices] IStorageService storageService, CancellationToken cancellationToken)
    {
      var userId = GetUserId();
      if (userId < 0) return UnauthorizedResponse();

      if (file == null || file.Length == 0) throw new AppException(ErrorCodes.INVALID_INPUT);

      using var stream = file.OpenReadStream();
      var folder = $"users/{userId}/banner";
      var url = await storageService.UploadAsync(stream, file.FileName, folder, cancellationToken);

      await _userService.UpdateProfileAsync(userId, new UpdateProfileDto { BannerUrl = url }, cancellationToken);

      return OkResponse(url);
    }
  }
}
