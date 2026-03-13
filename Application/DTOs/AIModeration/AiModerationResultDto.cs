using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Application.DTOs.AIModeration
{
    public class AiModerationResultDto
    {
        [JsonPropertyName("flagged")]
        public bool Flagged { get; set; }

        [JsonPropertyName("flaggedReason")]
        public string? FlaggedReason { get; set; }

        [JsonPropertyName("categoryScores")]
        public Dictionary<string, double> CategoryScores { get; set; } = new();
    }
}
