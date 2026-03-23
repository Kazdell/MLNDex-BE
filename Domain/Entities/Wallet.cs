using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class Wallet
  {
    public int WalletId { get; set; }
    public int UserId { get; set; }  // Unique constraint (1-1 with User)
    public decimal CoinBalance { get; set; }
    public decimal TotalEarned { get; set; }
    public decimal TotalSpent { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
  }
}
