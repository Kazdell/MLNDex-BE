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
}
