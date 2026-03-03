using Application.DTOs.Moderation;
using Application.Interfaces.AIModeration;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using mlndex_backend.Controllers;

namespace mlndex_backend.Controllers.Moderation
{
    [Route("api/[controller]")]
    public class ModerationController : BaseController
    {
        private readonly IModerationService _moderationService;

        public ModerationController(IModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        // Check text against blacklist and profanity filter.
        [HttpPost("check-text")]
        public IActionResult CheckText([FromBody] TextCheckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Text))
                return BadRequestResponse("Text content is required.");

            var result = _moderationService.PreCheckText(request);
            return OkResponse(result);
        }

        // Analyze OpenAI moderation scores against thresholds.
        [HttpPost("analyze-scores")]
        public IActionResult AnalyzeScores([FromBody] OpenAiScoreRequest request)
        {
            if (request.Scores == null || request.Scores.Count == 0)
                return BadRequestResponse("At least one category score is required.");

            var result = _moderationService.AnalyzeOpenAiScores(request);
            return OkResponse(result);
        }

        // Get all rejection templates for moderators.
        [HttpGet("templates")]
        public IActionResult GetTemplates()
        {
            return OkResponse(_moderationService.GetRejectionTemplates());
        }

        // Get all banned and restricted content tags.
        [HttpGet("banned-tags")]
        public IActionResult GetBannedTags()
        {
            return OkResponse(_moderationService.GetBannedTags());
        }
    }
}
