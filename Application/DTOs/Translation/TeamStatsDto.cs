namespace Application.DTOs.Translation
{
  public class TeamStatsDto
  {
    public int TotalViews { get; set; }
    public int TotalBookmarks { get; set; }
    public int ActiveSeriesCount { get; set; }
    public int TotalChaptersTranslated { get; set; }
    public decimal AverageRating { get; set; }
  }
}
