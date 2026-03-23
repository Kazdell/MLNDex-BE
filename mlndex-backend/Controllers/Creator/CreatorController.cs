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

    /// <summary>
    /// Đăng ký trở thành nhà sáng tạo.
    /// Tạo CreatorProfile (APPROVED + IsActive=true) và gán role Creator ngay lập tức.
    /// </summary>
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
        return BadRequest(new ApiResponse<string>(false, ex.Message));
      }
    }
  }
}
