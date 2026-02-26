using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers
{
    /// <summary>
    /// Moderation API Controller - Person #3 Content Policy Engine.
    /// Provides endpoints for text moderation, AI score analysis, and policy data retrieval.
    /// All endpoints are Mock-ready: work independently without OCR/OpenAI/Queue modules.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ModerationController : ControllerBase
    {
        private readonly IModerationService _moderationService;

        public ModerationController(IModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        /// <summary>
        /// Check text content against the blacklist and profanity filter.
        /// Accepts raw text (from OCR, comment input, or Light Novel content).
        /// </summary>
        /// <param name="request">Text content, comment flag, and user reputation score.</param>
        /// <returns>Moderation decision: AutoPass, FlagForReview, AutoReject, or InstantBan.</returns>
        [HttpPost("check-text")]
        public ActionResult<TextCheckResponse> CheckText([FromBody] TextCheckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
            {
                return BadRequest(new { message = "Text content is required." });
            }

            var result = _moderationService.PreCheckText(request);
            return Ok(result);
        }

        /// <summary>
        /// Analyze OpenAI omni-moderation scores against configured thresholds.
        /// Uses worst-score-wins logic. Zero-tolerance for CSAM (sexual/minors).
        /// </summary>
        /// <param name="request">Dictionary of category scores (0.0 - 1.0).</param>
        /// <returns>Final decision with age rating suggestion and reputation deduction.</returns>
        [HttpPost("analyze-scores")]
        public ActionResult<OpenAiScoreResponse> AnalyzeScores([FromBody] OpenAiScoreRequest request)
        {
            if (request.Scores == null || request.Scores.Count == 0)
            {
                return BadRequest(new { message = "At least one category score is required." });
            }

            var result = _moderationService.AnalyzeOpenAiScores(request);
            return Ok(result);
        }

        /// <summary>
        /// Retrieve all available rejection templates for moderators.
        /// </summary>
        /// <returns>List of 16 pre-configured rejection message templates.</returns>
        [HttpGet("templates")]
        public ActionResult<List<RejectionTemplateDto>> GetTemplates()
        {
            var templates = _moderationService.GetRejectionTemplates();
            return Ok(templates);
        }

        /// <summary>
        /// Retrieve all banned and restricted content tags.
        /// Banned tags result in auto-reject. Restricted tags apply age limits.
        /// </summary>
        /// <returns>Combined list of banned and restricted tags with their variants.</returns>
        [HttpGet("banned-tags")]
        public ActionResult<List<BannedTagDto>> GetBannedTags()
        {
            var tags = _moderationService.GetBannedTags();
            return Ok(tags);
        }
    }
}
