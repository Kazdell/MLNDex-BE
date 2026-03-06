using System;

namespace Application.DTOs.User
{
  public class ReadingHistoryUpdateDto
  {
    public int SeriesId { get; set; }
    public int ChapterId { get; set; }
    public int PageNumber { get; set; }
  }

  public class ReadingHistoryResponseDto
  {
    public int HistoryId { get; set; }
    public int SeriesId { get; set; }
    public string? SeriesTitle { get; set; }
    public int LastChapterId { get; set; }
    public float LastChapterNumber { get; set; }
    public int LastPageNumber { get; set; }
    public DateTime LastReadAt { get; set; }
  }
}
