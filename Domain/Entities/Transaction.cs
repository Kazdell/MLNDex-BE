using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace Domain.Entities
{
  public class Transaction
  {
    public int TransactionId { get; set; }
    public int UserId { get; set; }
    public int WalletId { get; set; }
    public TransactionType Type { get; set; }
    public decimal AmountCoins { get; set; }
    public int? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public TransactionStatus Status { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Wallet Wallet { get; set; } = null!;
    public ChapterUnlock? ChapterUnlock { get; set; }
  }

  public enum TransactionType
  {
    PURCHASE_COIN,
    CHAPTER_UNLOCK,
    WITHDRAWAL,
    REFUND,
    BONUS
  }

  public enum TransactionStatus
  {
    PENDING,
    COMPLETED,
    FAILED,
    REFUNDED
  }
}
