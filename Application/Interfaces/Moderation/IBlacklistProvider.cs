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
    void SetDynamicBlacklist(List<string> words);
    Task AddBlacklistWordAsync(string word, string category, string severity);
    Task<string> GetBlacklistJsonAsync();
    Task UpdateThresholdsAsync(Dictionary<string, ThresholdRule> thresholds);
    Task<Dictionary<string, ThresholdRule>> GetThresholdsAsync();
  }
}
