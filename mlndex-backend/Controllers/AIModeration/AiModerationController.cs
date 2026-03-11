using System.Security.Claims;
using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;

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
            await _moderationService.RunAiModerationAsync(chapterId);
            return OkResponse<object?>(null, "Kiểm duyệt hoàn tất");
        }

        // Submit an appeal for a chapter flagged by AI.
        [HttpPost("chapters/{chapterId}/appeal")]
        public async Task<IActionResult> SubmitAppeal(
            int chapterId,
            [FromBody] SubmitAppealRequestDto request
        )
        {
            if (string.IsNullOrWhiteSpace(request.AppealReason))
                return BadRequestResponse("Vui lòng nhập lý do appeal");

            var userId = GetUserId();
            if (userId == 0)
                return UnauthorizedResponse();

            await _moderationService.SubmitAppealAsync(chapterId, userId, request.AppealReason);

            return OkResponse<object?>(null, "Đã gửi yêu cầu xét duyệt lại thành công");
        }
    }
}
