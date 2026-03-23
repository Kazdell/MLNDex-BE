namespace Application.DTOs.Chapter
{
  public class ChapterSummaryDto
  {
    public int ChapterId { get; set; }
    public float ChapterNumber { get; set; }
    public string? Title { get; set; }
    public int? TeamId { get; set; }
    public string? TeamName { get; set; }
    public string? LanguageCode { get; set; }
    public string? LanguageName { get; set; }
    public bool IsOriginal { get; set; }
  }
}
