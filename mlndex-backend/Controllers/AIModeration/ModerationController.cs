using Application.DTOs.AIModeration;
using Application.DTOs.Moderation;
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

		// Check text against blacklist and profanity filter.
		[HttpPost("check-text")]
		public ActionResult<TextCheckResponse> CheckText([FromBody] TextCheckRequest request)
		{
			if (string.IsNullOrWhiteSpace(request.Text))
				return BadRequest(new { message = "Text content is required." });

			var result = _moderationService.PreCheckText(request);
			return Ok(result);
		}

		// Analyze OpenAI moderation scores against thresholds.
		[HttpPost("analyze-scores")]
		public ActionResult<OpenAiScoreResponse> AnalyzeScores([FromBody] OpenAiScoreRequest request)
		{
			if (request.Scores == null || request.Scores.Count == 0)
				return BadRequest(new { message = "At least one category score is required." });

			var result = _moderationService.AnalyzeOpenAiScores(request);
			return Ok(result);
		}

		// Get all rejection templates for moderators.
		[HttpGet("templates")]
		public ActionResult<List<RejectionTemplateDto>> GetTemplates()
		{
			return Ok(_moderationService.GetRejectionTemplates());
		}

		// Get all banned and restricted content tags.
		[HttpGet("banned-tags")]
		public ActionResult<List<BannedTagDto>> GetBannedTags()
		{
			return Ok(_moderationService.GetBannedTags());
		}
	}
}
