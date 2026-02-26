using Application.DTOs.Moderation;

namespace Application.Interfaces.Moderation
{
    // Core moderation service: blacklist check, AI score analysis, age rating.
    public interface IModerationService
    {
        TextCheckResponse PreCheckText(TextCheckRequest request);
        OpenAiScoreResponse AnalyzeOpenAiScores(OpenAiScoreRequest request);
        List<RejectionTemplateDto> GetRejectionTemplates();
        List<BannedTagDto> GetBannedTags();
    }
}
