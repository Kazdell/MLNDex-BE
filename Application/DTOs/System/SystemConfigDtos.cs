using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.System
{
  public class SystemConfigDto
  {
    [Range(0.01, 1000000000)]
    public decimal ExchangeRateCoinToVnd { get; set; } // 1 coin -> VND

    [Range(0, 100)]
    public decimal WithdrawalFeePercent { get; set; }

    [Range(0, 1000000000)]
    public decimal WithdrawalMinCoins { get; set; }

    [Range(0, 1000000000)]
    public decimal WithdrawalMaxCoins { get; set; }

    [Range(0, 100)]
    public decimal TranslationAuthorCommissionPercent { get; set; } = 70; // % hoa hồng trả cho tác giả khi mua bản dịch

    public List<string> BlacklistWords { get; set; } = new();

    public decimal MinWithdrawalAmountVnd { get; set; }
    
    public decimal MaxWithdrawalAmountVnd { get; set; }
  }

  public class AddBlacklistWordRequest
  {
    public string Word { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Severity { get; set; } = "high";
  }
}
