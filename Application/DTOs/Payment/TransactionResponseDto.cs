using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Payment
{
  /// <summary>1 dòng trong lịch sử giao dịch.</summary>
  public class TransactionResponseDto
  {
    public int TransactionId { get; set; }

    /// <summary>"TOP_UP" | "SPEND" | "EARN" | "WITHDRAW"</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Số coins biến động (luôn dương — chiều tăng/giảm xem Type).</summary>
    public decimal CoinAmount { get; set; }

    /// <summary>Số VND tương ứng. Chỉ có với TOP_UP và WITHDRAW, null với các loại khác.</summary>
    public long? AmountVnd { get; set; }

    /// <summary>"PENDING" | "SUCCESS" | "FAILED"</summary>
    public string Status { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Mã giao dịch phía cổng — để đối soát nếu cần.</summary>
    public string? GatewayTransactionId { get; set; }

    public DateTime CreatedAt { get; set; }
  }

  public class TransactionPagedResponseDto
  {
    public IEnumerable<TransactionResponseDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
  }
}
