using System.Text.Json.Serialization;

namespace Application.DTOs.AIModeration
{
    /// <summary>
    /// DTO returned by GET /moderation-status and pushed via SignalR.
    /// </summary>
    public class ModerationStatusDto
    {
        [JsonPropertyName("chapterId")]
        public int ChapterId { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("flagged")]
        public bool Flagged { get; set; }

        [JsonPropertyName("flaggedReason")]
        public string? FlaggedReason { get; set; }

        [JsonPropertyName("categoryScores")]
        public Dictionary<string, double>? CategoryScores { get; set; }

        [JsonPropertyName("updatedAt")]
        public DateTime? UpdatedAt { get; set; }
    }
}
