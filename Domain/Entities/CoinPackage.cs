using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
	public class CoinPackage
	{
		public int PackageId { get; set; }
		public string Name { get; set; } = null!;
		public decimal CoinAmount { get; set; }
		public decimal PriceVnd { get; set; }
		public decimal BonusCoins { get; set; }
		public bool IsActive { get; set; }
		public DateTime CreatedAt { get; set; }
	}
}
