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
        public decimal NetCoins { get; set; }
        public int TotalUnlocks { get; set; }
        public decimal TotalCoinPurchasedVnd { get; set; }
        public decimal TotalWithdrawVnd { get; set; }
        public decimal NetVnd { get; set; }
        public decimal ExchangeRateUsed { get; set; }
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
        public List<DailyRevenueDto> DailyRevenue { get; set; } = new();
    }

    public class DailyRevenueDto
    {
        public string Date { get; set; } = string.Empty;
        public decimal Purchased { get; set; }
        public decimal Withdrawn { get; set; }
    }
}
