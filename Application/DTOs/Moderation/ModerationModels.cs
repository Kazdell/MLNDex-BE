using System.Text.Json.Serialization;

namespace Application.DTOs.Moderation
{
  public class BlacklistEntry
  {
    public string Word { get; set; } = string.Empty;
    public List<string> Variants { get; set; } = new();
    public string Severity { get; set; } = "low";
  }

  public class ThresholdRule
  {
    [JsonPropertyName("AUTO_REJECT")]
    public double AUTO_REJECT { get; set; }
    
    [JsonPropertyName("FLAG_FOR_REVIEW")]
    public double FLAG_FOR_REVIEW { get; set; }
  }
}
