using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Payment
{
  /// <summary>
  /// Admin xác nhận chuyển khoản ngân hàng thủ công sau khi đối soát sao kê.
  /// </summary>
  public class ConfirmBankTransferDto
  {
    [Required(ErrorMessage = "TxnRef không được để trống")]
    public string TxnRef { get; set; } = string.Empty;

    /// <summary>Mã giao dịch từ sao kê ngân hàng — lưu để đối soát.</summary>
    [Required(ErrorMessage = "Mã giao dịch ngân hàng không được để trống")]
    public string BankTransactionId { get; set; } = string.Empty;

    public string? Note { get; set; }
  }
}
