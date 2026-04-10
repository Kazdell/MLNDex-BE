using Application.DTOs.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Payment
{
  /// <summary>Thông tin 1 gói coin Admin tạo sẵn.</summary>
  public class CoinPackageResponseDto
  {
    public int PackageId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PriceCoins { get; set; }
    public decimal CoinAmount { get; set; }
    public decimal BonusCoins { get; set; }
    public decimal TotalCoins => CoinAmount + BonusCoins;
    public bool IsActive { get; set; }
  }

  /// <summary>Admin tạo gói coin mới.</summary>
  public class CreateCoinPackageDto
  {
    [Required(ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, double.MaxValue, ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public decimal CoinAmount { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public decimal PriceCoins { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = ErrorCodes.VALIDATION_ERROR)]
    public decimal BonusCoins { get; set; } = 0;
  }

  /// <summary>Admin cập nhật gói coin — tất cả fields đều optional.</summary>
  public class UpdateCoinPackageDto
  {
    [MaxLength(100)]
    public string? Name { get; set; }
    public decimal? CoinAmount { get; set; }
    public decimal? PriceCoins { get; set; }
    public decimal? BonusCoins { get; set; }
    public bool? IsActive { get; set; }
  }
}

