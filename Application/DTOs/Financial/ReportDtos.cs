using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Financial
{
  public class FinancialReportRequest
  {
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    [Range(1, 100)]
    public int TopCreators { get; set; } = 10;
  }

  public class FinancialSummaryDto
  {
    public decimal TotalCoinPurchased { get; set; }
    public decimal TotalWithdrawCoins { get; set; }
    public decimal NetCoins => TotalCoinPurchased - TotalWithdrawCoins;
    public int TotalUnlocks { get; set; }
  }

  public class CreatorRevenueDto
  {
    public int CreatorId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public decimal CoinsEarned { get; set; }
    public int UnlockCount { get; set; }
  }

  public class FinancialReportResponse
  {
    public FinancialSummaryDto Summary { get; set; } = new();
    public List<CreatorRevenueDto> TopCreators { get; set; } = new();
  }
}
