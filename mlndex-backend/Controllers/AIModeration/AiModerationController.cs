using Application.DTOs.Common;
using Application.Exceptions;
using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;
using System.Security.Claims;

namespace mlndex_backend.Controllers.AIModeration
{
  [Route("api/[controller]")]
  [ApiController]
  public class AiModerationController : BaseController
  {
    private readonly IModerationService _moderationService;

    public AiModerationController(IModerationService moderationService)
    {
      _moderationService = moderationService;
    }

    // Trigger AI moderation for a chapter. Called internally after upload.
    [HttpPost("chapters/{chapterId}/run")]
    public async Task<IActionResult> RunAiModeration(int chapterId)
    {
      var result = await _moderationService.RunAiModerationAsync(chapterId);
      return OkResponse(result, "Kiểm duyệt hoàn tất");
    }

    [HttpPost("series/{seriesId}/run")]
    public async Task<IActionResult> RunSeriesModeration(int seriesId)
    {
      var result = await _moderationService.RunSeriesModerationAsync(seriesId);
      return OkResponse(result, "Kiểm duyệt hoàn tất");
    }

    // Submit an appeal for a chapter flagged by AI.
    [HttpPost("chapters/{chapterId}/appeal")]
    public async Task<IActionResult> SubmitAppeal(
        int chapterId,
        [FromBody] SubmitAppealRequestDto request
    )
    {
      if (string.IsNullOrWhiteSpace(request.AppealReason))
        throw new AppException(ErrorCodes.INVALID_INPUT);

      var userId = GetUserId();
      if (userId < 0)
        return UnauthorizedResponse();

      await _moderationService.SubmitAppealAsync(chapterId, userId, request.AppealReason);

      return OkResponse<object?>(null, "Đã gửi yêu cầu xét duyệt lại thành công");
    }
  }
}
