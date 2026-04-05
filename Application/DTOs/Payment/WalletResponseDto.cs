namespace Application.DTOs.Payment
{

  /// <summary>Trạng thái ví của user — hiển thị trên trang /wallet.</summary>
  public class WalletResponseDto
  {
    public int WalletId { get; set; }
    public decimal CoinBalance { get; set; }
    public decimal TotalEarned { get; set; }
    public decimal TotalSpent { get; set; }
  }
}
