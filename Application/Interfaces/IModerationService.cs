using Application.DTOs.Moderation;


namespace Application.Interfaces
{
    /// <summary>
    /// Core moderation service interface.
    /// Implements Person #3's rules: blacklist check, OpenAI threshold analysis, age rating.
    /// </summary>
    public interface IModerationService
    {
        /// <summary>
        /// Pre-check text content against blacklist (Person #3 rules).
        /// Called after OCR extraction or for direct text input (comments, Light Novels).
        /// </summary>
        /// <param name="request">Text content, comment flag, and user reputation.</param>
        /// <returns>Moderation decision with action, reasons, and penalty points.</returns>
        TextCheckResponse PreCheckText(TextCheckRequest request);

        /// <summary>
        /// Analyze OpenAI omni-moderation scores against configured thresholds.
        /// Uses worst-score-wins logic with zero-tolerance for CSAM.
        /// </summary>
        /// <param name="request">Dictionary of category scores (0.0 - 1.0).</param>
        /// <returns>Final decision including age rating suggestion.</returns>
        OpenAiScoreResponse AnalyzeOpenAiScores(OpenAiScoreRequest request);

        /// <summary>
        /// Get all available rejection templates for moderators.
        /// </summary>
        List<RejectionTemplateDto> GetRejectionTemplates();

        /// <summary>
        /// Get all banned and restricted content tags.
        /// </summary>
        List<BannedTagDto> GetBannedTags();
    }
}
