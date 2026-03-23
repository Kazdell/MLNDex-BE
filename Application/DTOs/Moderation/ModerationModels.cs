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
    public double AUTO_REJECT { get; set; }
    public double FLAG_FOR_REVIEW { get; set; }
  }
}
