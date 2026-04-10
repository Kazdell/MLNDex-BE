using Application.Exceptions;
using Application.DTOs.Common;
using Application.DTOs.Creator;
using Application.Interfaces.Creator;
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

    public CreatorController(ICreatorService creatorService)
    {
      _creatorService = creatorService;
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
      catch (InvalidOperationException ex)
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
  }
}
