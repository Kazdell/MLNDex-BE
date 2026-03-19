using System;

namespace Application.DTOs.User
{
    // === Request DTOs ===
    public class RatingRequestDto
    {
        public int SeriesId { get; set; }
        public int Score { get; set; } // 1-10
        public string? Review { get; set; }
    }

    // === Response DTOs ===
    public class RatingResponseDto
    {
        public int RatingId { get; set; }
        public int SeriesId { get; set; }
        public int Score { get; set; }
        public string? Review { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SeriesRatingSummaryDto
    {
        public decimal AverageRating { get; set; }
        public int TotalRatings { get; set; }
        public int? UserScore { get; set; } // null if user hasn't rated
    }
}
