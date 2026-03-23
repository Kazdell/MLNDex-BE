using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class ChapterUnlock
  {
    public int UnlockId { get; set; }
    public int ChapterId { get; set; }
    public int UserId { get; set; }
    public int TransactionId { get; set; }
    public decimal CoinsPaid { get; set; }
    public UnlockSource UnlockSource { get; set; }

    // Navigation
    public Chapter Chapter { get; set; } = null!;
    public User User { get; set; } = null!;
    public Transaction Transaction { get; set; } = null!;
  }

  public enum UnlockSource
  {
    COIN,
    VIP_AUTO,
    ADMIN_GRANT,
    PROMO
  }
}
