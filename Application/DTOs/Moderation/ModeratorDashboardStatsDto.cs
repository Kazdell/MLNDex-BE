using Domain.Entities;

namespace Application.DTOs.Moderation
{
  public class ModeratorDashboardStatsDto
  {
    public int NewReports { get; set; }
    public int ProcessingReports { get; set; }
    public int ResolvedReports { get; set; }
    public int TotalReports { get; set; }

    public List<DailyModerationStatDto> WeekData { get; set; } = new();
    public List<SystemActivityDto> Activities { get; set; } = new();
  }

  public class DailyModerationStatDto
  {
    public string Day { get; set; } = string.Empty;
    public int Incoming { get; set; }
    public int Processed { get; set; }
  }

  public class SystemActivityDto
  {
    public string ModeratorName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty; // e.g. "bg-green-500", "bg-red-500"
    public string Text { get; set; } = string.Empty;
  }
}
