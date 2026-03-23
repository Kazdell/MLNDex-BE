using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class WithdrawalRequest
  {
    public int WithdrawalId { get; set; }
    public int CreatorId { get; set; }
    public decimal AmountCoins { get; set; }
    public decimal AmountVnd { get; set; }
    public string BankAccountInfo { get; set; } = null!;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public WithdrawalStatus Status { get; set; }
    public string? Note { get; set; }

    // Navigation
    public CreatorProfile Creator { get; set; } = null!;
  }

  public enum WithdrawalStatus
  {
    PENDING,
    PROCESSING,
    COMPLETED,
    REJECTED
  }
}
