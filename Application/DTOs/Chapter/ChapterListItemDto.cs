namespace Application.DTOs.Chapter
{
  public class ChapterListItemDto
  {
    public int ChapterId { get; set; }
    public float ChapterNumber { get; set; }
    public string? Title { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ModerationStatus { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public int Views { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime CreatedAt { get; set; }
  }
}
