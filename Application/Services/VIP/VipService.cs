using Application.DTOs.Common;
using Application.Exceptions;
using Application.DTOs.VIP;
using Application.Interfaces.Data;
using Application.Interfaces.VIP;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services.VIP
{
	// Application/Services/VipService.cs
	public class VipService : IVipService
	{
		private readonly IMlndexDbContext _context;

		public VipService(IMlndexDbContext context)
		{
			_context = context;
		}

		// Lấy danh sách gói VIP
		public async Task<List<VipPlanDto>> GetActivePlansAsync()
		{
			return await _context.VipPlans
				.Where(p => p.IsActive)
				.OrderBy(p => p.PriceCoins)
				.Select(p => new VipPlanDto
				{
					PlanId = p.PlanId,
					Name = p.Name,
					Description = p.Description,
					PriceCoins = p.PriceCoins,
					DurationDays = p.DurationDays,
					AutoUnlockChapter = p.AutoUnlockChapter,
					IsActive = p.IsActive
				})
				.ToListAsync();
		}

		// Check user có VIP active không
		public async Task<bool> IsUserVipAsync(int userId)
		{
			return await _context.VipSubscriptions
				.AnyAsync(s => s.UserId == userId
							&& s.Status == SubscriptionStatus.ACTIVE
							&& s.StartDate <= DateTime.UtcNow
							&& s.EndDate > DateTime.UtcNow);
		}

		// Lấy subscription đang active
		public async Task<VipSubscriptionDto?> GetActiveSubscriptionAsync(int userId)
		{
			var sub = await _context.VipSubscriptions
				.Include(s => s.VipPlan)
				.Where(s => s.UserId == userId
						 && s.Status == SubscriptionStatus.ACTIVE
						 && s.StartDate <= DateTime.UtcNow
						 && s.EndDate > DateTime.UtcNow)
				.OrderByDescending(s => s.EndDate)
				.FirstOrDefaultAsync();

			return sub == null ? null : MapToDto(sub);
		}

		// Lịch sử subscription
		public async Task<List<VipSubscriptionDto>> GetSubscriptionHistoryAsync(int userId)
		{
			var subs = await _context.VipSubscriptions
				.Include(s => s.VipPlan)
				.Where(s => s.UserId == userId)
				.OrderByDescending(s => s.StartDate)
				.ToListAsync();

			return subs.Select(MapToDto).ToList();
		}

		// Mua VIP
		public async Task<PurchaseVipResponseDto> PurchaseVipAsync(
			int userId, PurchaseVipRequestDto request)
		{
			// 1. Validate plan
			var plan = await _context.VipPlans
				.FirstOrDefaultAsync(p => p.PlanId == request.PlanId && p.IsActive)
				?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.VIP_PACKAGE_NOT_FOUND,
					"Gói VIP không tồn tại hoặc đã ngừng hoạt động.");

			// 2. Validate wallet
			var wallet = await _context.Wallets
				.FirstOrDefaultAsync(w => w.UserId == userId)
				?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

			if (wallet.CoinBalance < plan.PriceCoins)
				throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.INSUFFICIENT_BALANCE,
					$"Không đủ coins. Cần {plan.PriceCoins} coins, " +
					$"hiện có {wallet.CoinBalance} coins.");

			// 3. Tính StartDate — nối tiếp nếu đang có sub active
			var latestActiveSub = await _context.VipSubscriptions
				.Where(s => s.UserId == userId
						 && s.Status == SubscriptionStatus.ACTIVE
						 && s.EndDate > DateTime.UtcNow)
				.OrderByDescending(s => s.EndDate)
				.FirstOrDefaultAsync();

			DateTime startDate = latestActiveSub == null
				? DateTime.UtcNow
				: latestActiveSub.EndDate;
			DateTime endDate = startDate.AddDays(plan.DurationDays);

			// 4. Trừ coins
			wallet.CoinBalance -= plan.PriceCoins;
			wallet.TotalSpent += plan.PriceCoins;

			// 5. Ghi Transaction
			var transaction = new Transaction
			{
				UserId = userId,
				WalletId = wallet.WalletId,
				Type = TransactionType.VIP_PURCHASE,
				AmountCoins = -plan.PriceCoins,
				RelatedEntityId = plan.PlanId,
				RelatedEntityType = "VipPlan",
				Status = TransactionStatus.COMPLETED,
				Note = $"Mua gói VIP: {plan.Name}",
				CreatedAt = DateTime.UtcNow
			};
			_context.Transactions.Add(transaction);

			// 6. Tạo VipSubscription
			var subscription = new VipSubscription
			{
				UserId = userId,
				PlanId = plan.PlanId,
				StartDate = startDate,
				EndDate = endDate,
				PricePaid = plan.PriceCoins,
				AutoRenew = request.AutoRenew,
				Status = SubscriptionStatus.ACTIVE
			};
			_context.VipSubscriptions.Add(subscription);

			await _context.SaveChangesAsync();

			return new PurchaseVipResponseDto
			{
				SubscriptionId = subscription.SubscriptionId,
				PlanName = plan.Name,
				StartDate = startDate,
				EndDate = endDate,
				CoinsDeducted = plan.PriceCoins,
				RemainingCoins = wallet.CoinBalance
			};
		}

		// Huỷ subscription
		public async Task<VipSubscriptionDto> CancelSubscriptionAsync(
			int userId, int subscriptionId)
		{
			var sub = await _context.VipSubscriptions
				.Include(s => s.VipPlan)
				.FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId
									   && s.UserId == userId)
				?? throw new AppException(ErrorCodes.SUBSCRIPTION_NOT_FOUND);

			if (sub.Status != SubscriptionStatus.ACTIVE)
				throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED,
					"Chỉ có thể huỷ subscription đang ACTIVE.");

			// Vẫn còn hiệu lực đến EndDate, chỉ tắt AutoRenew và đánh dấu CANCELLED
			sub.AutoRenew = false;
			sub.Status = SubscriptionStatus.CANCELLED;
			await _context.SaveChangesAsync();

			return MapToDto(sub);
		}

		// Admin lấy tất cả gói kể cả inactive
		public async Task<List<VipPlanDto>> GetAllPlansAsync()
		{
			return await _context.VipPlans
				.OrderBy(p => p.PriceCoins)
				.Select(p => new VipPlanDto
				{
					PlanId = p.PlanId,
					Name = p.Name,
					Description = p.Description,
					PriceCoins = p.PriceCoins,
					DurationDays = p.DurationDays,
					AutoUnlockChapter = p.AutoUnlockChapter,
					IsActive = p.IsActive
				})
				.ToListAsync();
		}

		public async Task<VipPlanDto> CreatePlanAsync(CreateVipPlanDto request)
		{
			// Không cho tạo 2 gói trùng tên
			var exists = await _context.VipPlans
				.AnyAsync(p => p.Name == request.Name);
			if (exists)
				throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.VALIDATION_ERROR,
					$"Gói VIP tên '{request.Name}' đã tồn tại.");

			var plan = new VipPlan
			{
				Name = request.Name,
				Description = request.Description,
				PriceCoins = request.PriceCoins,
				DurationDays = request.DurationDays,
				AutoUnlockChapter = request.AutoUnlockChapter,
				IsActive = true // mặc định active khi tạo mới
			};

			_context.VipPlans.Add(plan);
			await _context.SaveChangesAsync();

			return new VipPlanDto
			{
				PlanId = plan.PlanId,
				Name = plan.Name,
				Description = plan.Description,
				PriceCoins = plan.PriceCoins,
				DurationDays = plan.DurationDays,
				AutoUnlockChapter = plan.AutoUnlockChapter,
				IsActive = plan.IsActive
			};
		}

		public async Task<VipPlanDto> UpdatePlanAsync(int planId, UpdateVipPlanDto request)
		{
			var plan = await _context.VipPlans
				.FirstOrDefaultAsync(p => p.PlanId == planId)
				?? throw new AppException(ErrorCodes.VIP_PACKAGE_NOT_FOUND);

			// Chỉ cập nhật field nào admin gửi lên
			if (request.Name != null) plan.Name = request.Name;
			if (request.Description != null) plan.Description = request.Description;
			if (request.PriceCoins.HasValue) plan.PriceCoins = request.PriceCoins.Value;
			if (request.DurationDays.HasValue) plan.DurationDays = request.DurationDays.Value;
			if (request.AutoUnlockChapter.HasValue) plan.AutoUnlockChapter = request.AutoUnlockChapter.Value;
			if (request.IsActive.HasValue) plan.IsActive = request.IsActive.Value;

			await _context.SaveChangesAsync();

			return new VipPlanDto
			{
				PlanId = plan.PlanId,
				Name = plan.Name,
				Description = plan.Description,
				PriceCoins = plan.PriceCoins,
				DurationDays = plan.DurationDays,
				AutoUnlockChapter = plan.AutoUnlockChapter,
				IsActive = plan.IsActive
			};
		}

		public async Task DeletePlanAsync(int planId)
		{
			var plan = await _context.VipPlans
				.FirstOrDefaultAsync(p => p.PlanId == planId)
				?? throw new AppException(ErrorCodes.VIP_PACKAGE_NOT_FOUND);

			// Không cho xoá nếu đang có user dùng gói này
			var hasActiveSubs = await _context.VipSubscriptions
				.AnyAsync(s => s.PlanId == planId
							&& s.Status == SubscriptionStatus.ACTIVE
							&& s.EndDate > DateTime.UtcNow);
			if (hasActiveSubs)
				throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED,
					"Không thể xoá gói VIP đang có user sử dụng. " +
					"Hãy tắt IsActive thay vì xoá.");

			_context.VipPlans.Remove(plan);
			await _context.SaveChangesAsync();
		}

		public async Task<bool> CanUserReadChapterAsync(
	int chapterId, int? userId, CancellationToken cancellationToken = default)
		{
			var chapter = await _context.Chapters
				.FirstOrDefaultAsync(c => c.ChapterId == chapterId, cancellationToken);
			if (chapter == null) return false;

			// UNLOCKED — ai cũng đọc được
			if (chapter.LockStatus == ChapterLockStatus.UNLOCKED) return true;

			// LOCKED — chưa login thì không đọc được
			if (!userId.HasValue) return false;

			// Check VIP active + AutoUnlockChapter
			var hasVip = await _context.VipSubscriptions
				.Include(s => s.VipPlan)
				.AnyAsync(s => s.UserId == userId.Value
							&& s.Status == SubscriptionStatus.ACTIVE
							&& s.StartDate <= DateTime.UtcNow
							&& s.EndDate > DateTime.UtcNow
							&& s.VipPlan.AutoUnlockChapter,
						  cancellationToken);
			if (hasVip) return true;

			// Check đã unlock lẻ bằng coin
			return await _context.ChapterUnlocks
				.AnyAsync(u => u.ChapterId == chapterId
							&& u.UserId == userId.Value,
						  cancellationToken);
		}
		// ── Helper ───────────────────────────────────────────────────────────────

		private static VipSubscriptionDto MapToDto(VipSubscription s) => new()
		{
			SubscriptionId = s.SubscriptionId,
			PlanId = s.PlanId,
			PlanName = s.VipPlan?.Name ?? string.Empty,
			StartDate = s.StartDate,
			EndDate = s.EndDate,
			PricePaid = s.PricePaid,
			AutoRenew = s.AutoRenew,
			Status = s.Status.ToString(),
			IsCurrentlyActive = s.Status == SubscriptionStatus.ACTIVE
							 && s.StartDate <= DateTime.UtcNow
							 && s.EndDate > DateTime.UtcNow
		};
	}
}
