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

    [JsonPropertyName("perPageResults")]
    public List<PageModerationDto>? PerPageResults { get; set; }

    [JsonPropertyName("scanMode")]
    public string? ScanMode { get; set; }
  }

  public class PageModerationDto
  {
    [JsonPropertyName("pageNumber")]
    public int PageNumber { get; set; }

    [JsonPropertyName("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("flagged")]
    public bool Flagged { get; set; }

    [JsonPropertyName("categoryScores")]
    public Dictionary<string, double> CategoryScores { get; set; } = new();
  }
}
