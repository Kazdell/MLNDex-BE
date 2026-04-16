using Application.DTOs.VIP;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.VIP
{
	public interface IVipService
	{
		Task<List<VipPlanDto>> GetActivePlansAsync();
		Task<VipSubscriptionDto?> GetActiveSubscriptionAsync(int userId);
		Task<List<VipSubscriptionDto>> GetSubscriptionHistoryAsync(int userId);
		Task<PurchaseVipResponseDto> PurchaseVipAsync(int userId, PurchaseVipRequestDto request);
		Task<VipSubscriptionDto> CancelSubscriptionAsync(int userId, int subscriptionId);
		Task<bool> IsUserVipAsync(int userId);
		Task<VipPlanDto> CreatePlanAsync(CreateVipPlanDto request);
		Task<VipPlanDto> UpdatePlanAsync(int planId, UpdateVipPlanDto request);
		Task DeletePlanAsync(int planId);
		Task<List<VipPlanDto>> GetAllPlansAsync();
		Task<bool> CanUserReadChapterAsync(int chapterId, int? userId, CancellationToken cancellationToken = default);
		Task<VipSubscriptionDto> ToggleAutoRenewAsync(int userId, int subscriptionId);
	}
}
