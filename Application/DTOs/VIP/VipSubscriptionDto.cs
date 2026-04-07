using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.VIP
{
	public class VipSubscriptionDto
	{
		public int SubscriptionId { get; set; }
		public int PlanId { get; set; }
		public string PlanName { get; set; } = null!;
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public decimal PricePaid { get; set; }
		public bool AutoRenew { get; set; }
		public string Status { get; set; } = null!;
		public bool IsCurrentlyActive { get; set; }
	}

	/// <summary>
	/// Request từ user khi muốn mua VIP bằng coins
	/// </summary>
	public class PurchaseVipRequestDto
	{
		public int PlanId { get; set; }
		public bool AutoRenew { get; set; }
	}

	/// <summary>
	/// Trả về sau khi mua VIP thành công
	/// </summary>
	public class PurchaseVipResponseDto
	{
		public int SubscriptionId { get; set; }
		public string PlanName { get; set; } = null!;
		public DateTime StartDate { get; set; }
		public DateTime EndDate { get; set; }
		public decimal CoinsDeducted { get; set; }
		public decimal RemainingCoins { get; set; }
	}
}
