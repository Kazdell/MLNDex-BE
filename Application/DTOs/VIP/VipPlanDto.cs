using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.VIP
{
	public class VipPlanDto
	{
		public int PlanId { get; set; }
		public string Name { get; set; } = null!;
		public string? Description { get; set; }
		public decimal PriceCoins { get; set; }
		public int DurationDays { get; set; }
		public bool AutoUnlockChapter { get; set; }
		public bool IsActive { get; set; }
	}
}
