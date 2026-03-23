using System;

namespace Application.DTOs.User
{
  public class ReadingHistoryDto
  {
    public int SeriesId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CoverUrl { get; set; } = string.Empty;
    public int LastChapterId { get; set; }
    public string LastChapterTitle { get; set; } = string.Empty;
    public int LastPageNumber { get; set; }
    public DateTime LastReadAt { get; set; }
  }

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
    public string Title { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public int LastChapterId { get; set; }
    public string LastChapterTitle { get; set; } = string.Empty;
    public int LastPageNumber { get; set; }
    public int Progress { get; set; } = 100;
    public DateTime LastReadAt { get; set; }
  }

  public class ReadingStatsDto
  {
    public int TotalSeriesRead { get; set; }
    public int TotalChaptersRead { get; set; }
    public int TotalFollowing { get; set; }
    public int TotalRated { get; set; }
    public int TotalBookmarks { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public List<GenreStatDto> TopGenres { get; set; } = new List<GenreStatDto>();
    public List<MonthlyActivityDto> MonthlyActivity { get; set; } = new List<MonthlyActivityDto>();
    public List<ReadingHistoryResponseDto> RecentActivity { get; set; } = new List<ReadingHistoryResponseDto>();
  }

  public class GenreStatDto
  {
    public string Genre { get; set; } = string.Empty;
    public int Count { get; set; }
  }

  public class MonthlyActivityDto
  {
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
  }
}
