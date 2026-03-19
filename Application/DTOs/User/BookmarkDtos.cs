using System;

namespace Application.DTOs.User
{
    // === Request DTOs ===
    public class BookmarkRequestDto
    {
        public int SeriesId { get; set; }
        public int? ChapterId { get; set; }
        public string? Note { get; set; }
    }

    // === Response DTOs ===
    public class BookmarkResponseDto
    {
        public int BookmarkId { get; set; }
        public int SeriesId { get; set; }
        public string SeriesTitle { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public int? ChapterId { get; set; }
        public string? ChapterTitle { get; set; }
        public string? Note { get; set; }
        public DateTime BookmarkedAt { get; set; }
    }
}
