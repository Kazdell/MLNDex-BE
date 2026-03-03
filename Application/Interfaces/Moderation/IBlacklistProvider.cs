using Application.DTOs.Moderation;

namespace Application.Interfaces.Moderation
{
    public interface IBlacklistProvider
    {
        List<BlacklistEntry> ProfanityList { get; }
        List<BlacklistEntry> HateSpeechList { get; }
        List<BlacklistEntry> IllegalContentList { get; }
        List<RejectionTemplateDto> RejectionTemplates { get; }
        List<BannedTagDto> BannedTags { get; }
        List<BannedTagDto> RestrictedTags { get; }
        Dictionary<string, ThresholdRule> Thresholds { get; }
        void LoadAll();
    }
}
