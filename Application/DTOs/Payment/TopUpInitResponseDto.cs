using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Payment
{
  /// <summary>
  /// Trả về sau khi tạo giao dịch pending thành công.
  /// Frontend dùng PaymentUrl để redirect user sang gateway,
  /// hoặc hiển thị BankInfo nếu là chuyển khoản.
  /// </summary>
  public class TopUpInitResponseDto
  {
    public string TxnRef { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public long AmountVnd { get; set; }
    public long CoinsWillReceive { get; set; }
    public DateTime ExpiredAt { get; set; }

    /// <summary>
    /// URL redirect sang trang thanh toán.
    /// Có giá trị với VNPAY và MOMO.
    /// </summary>
    public string? PaymentUrl { get; set; }
  }
}
