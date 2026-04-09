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
    public decimal PriceVnd{ get; set; }
    public decimal CoinAmount { get; set; }
    public decimal BonusCoins { get; set; }
    public decimal TotalCoins => CoinAmount + BonusCoins;
    public bool IsActive { get; set; }
  }

  /// <summary>Admin tạo gói coin mới.</summary>
  public class CreateCoinPackageDto
  {
    [Required(ErrorMessage = "Tên gói không được để trống")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, double.MaxValue, ErrorMessage = "Số coins phải lớn hơn 0")]
    public decimal CoinAmount { get; set; }

    [Range(1, double.MaxValue, ErrorMessage = "Giá tiền phải lớn hơn 0")]
    public decimal PriceVnd { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Bonus coins không được âm")]
    public decimal BonusCoins { get; set; } = 0;
  }

  /// <summary>Admin cập nhật gói coin — tất cả fields đều optional.</summary>
  public class UpdateCoinPackageDto
  {
    [MaxLength(100)]
    public string? Name { get; set; }
    public decimal? CoinAmount { get; set; }
    public decimal? PriceVnd { get; set; }
    public decimal? BonusCoins { get; set; }
    public bool? IsActive { get; set; }
  }
}
