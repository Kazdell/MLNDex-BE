using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace mlndex_backend.Controllers.AIModeration
{
	[Route("api/Moderation")]
	[ApiController]
	public class ModerationController : BaseController
	{
		private readonly IModerationService _moderationService;

		public ModerationController(IModerationService moderationService)
		{
			_moderationService = moderationService;
		}

		// ─────────────────────────────────────────────────────────────
		// POST /api/moderation/chapters/{chapterId}/run
		// Trigger AI kiểm duyệt - gọi nội bộ sau khi upload chapter xong
		// Chỉ Creator hoặc System mới được gọi
		// ─────────────────────────────────────────────────────────────
		[HttpPost("chapters/{chapterId}/run")]
		//[Authorize(Roles = "CREATOR")]
		public async Task<IActionResult> RunAiModeration(int chapterId)
		{
			await _moderationService.RunAiModerationAsync(chapterId);
			return OkResponse<object>(null, "Kiểm duyệt hoàn tất");
		}

		// ─────────────────────────────────────────────────────────────
		// POST /api/moderation/chapters/{chapterId}/appeal
		// Tác giả gửi yêu cầu review lại sau khi bị AI flag
		// ─────────────────────────────────────────────────────────────
		[HttpPost("chapters/{chapterId}/appeal")]
		//[Authorize(Roles = "CREATOR")]
		public async Task<IActionResult> SubmitAppeal(int chapterId, [FromBody] SubmitAppealRequestDto request)
		{
			if (string.IsNullOrWhiteSpace(request.AppealReason))
				return BadRequestResponse("Vui lòng nhập lý do appeal");

			var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);

			if (userIdClaim is null)
				return UnauthorizedResponse();

			var userId = int.Parse(userIdClaim);
			//var userId = 1;

			await _moderationService.SubmitAppealAsync(chapterId, userId, request.AppealReason);

			return OkResponse<object>(null, "Đã gửi yêu cầu xét duyệt lại thành công");
		}
	}
}
