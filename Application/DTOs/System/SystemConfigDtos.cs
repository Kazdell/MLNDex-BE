using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.System
{
  public class SystemConfigDto
  {
    [Range(0.01, double.MaxValue)]
    public decimal ExchangeRateCoinToVnd { get; set; } // 1 coin -> VND

    [Range(0, 100)]
    public decimal WithdrawalFeePercent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal WithdrawalMinCoins { get; set; }

    [Range(0, double.MaxValue)]
    public decimal WithdrawalMaxCoins { get; set; }

    [Range(0, 100)]
    public decimal TranslationAuthorCommissionPercent { get; set; } = 70; // % hoa hồng trả cho tác giả khi mua bản dịch

    public List<string> BlacklistWords { get; set; } = new();
  }
}
