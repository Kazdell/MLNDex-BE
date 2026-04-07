namespace Application.DTOs.Chapter
{
  public class ChapterSummaryDto
  {
    public int ChapterId { get; set; }
    public int? TranslationId { get; set; }
    public float ChapterNumber { get; set; }
    public string? Title { get; set; }
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? LanguageCode { get; set; }
    public string? LanguageName { get; set; }
    public bool IsOriginal { get; set; }

    public string LockStatus { get; set; } = "UNLOCKED";
    public int? UnlockPriceCoins { get; set; }
    public DateTime? UnlockTime { get; set; }
    public bool IsUnlockedByUser { get; set; } = false;
  }
}
