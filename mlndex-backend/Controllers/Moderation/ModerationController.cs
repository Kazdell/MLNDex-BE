using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Moderation
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModerationController : ControllerBase
    {
        private readonly IModerationService _moderationService;

        public ModerationController(IModerationService moderationService)
        {
            _moderationService = moderationService;
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
