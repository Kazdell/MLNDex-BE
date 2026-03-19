using System;

namespace Application.DTOs.User
{
    // === Request DTOs ===
    public class FollowRequestDto
    {
        public int TargetId { get; set; }
        public string TargetType { get; set; } = "SERIES"; // SERIES | CREATOR | TEAM
    }

    // === Response DTOs ===
    public class FollowResponseDto
    {
        public int FollowId { get; set; }
        public int TargetId { get; set; }
        public string TargetType { get; set; } = string.Empty;
        public DateTime FollowedAt { get; set; }
    }

    public class FollowStatusDto
    {
        public bool IsFollowing { get; set; }
        public int? FollowId { get; set; }
    }

    public class FollowedSeriesDto
    {
        public int FollowId { get; set; }
        public int SeriesId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public string Status { get; set; } = string.Empty;
        public decimal AverageRating { get; set; }
        public DateTime FollowedAt { get; set; }
        public string? CreatorName { get; set; }
        public string? LatestChapter { get; set; }
    }
}
