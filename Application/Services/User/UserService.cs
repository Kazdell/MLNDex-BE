using Application.DTOs.User;
using Application.Interfaces.Data;
using Application.Interfaces.User;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Services.User
{
    public class UserService : IUserService
    {
        private readonly IMlndexDbContext _context;

        public UserService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<UserProfileDto?> GetProfileAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.Wallet)
                .Include(u => u.ReadingHistories)
                .Include(u => u.VipSubscriptions).ThenInclude(vs => vs.VipPlan)
                .Include(u => u.CreatorProfile).ThenInclude(cp => cp.Series)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (user == null) return null;

            // Lấy subscription đang hoạt động
            var activeSubscription = user.VipSubscriptions
                .Where(vs => vs.Status == SubscriptionStatus.ACTIVE && vs.EndDate > DateTime.UtcNow)
                .OrderByDescending(vs => vs.EndDate)
                .FirstOrDefault();

            var followersCount = user.CreatorProfile != null 
                ? await _context.Follows.CountAsync(f => f.TargetId == user.CreatorProfile.CreatorId && f.TargetType == FollowTargetType.CREATOR, cancellationToken)
                : 0;
            
            var followingCount = await _context.Follows.CountAsync(f => f.UserId == userId, cancellationToken);

            return new UserProfileDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                Avatar = user.DisplayAvatar,
                CreatedAt = user.CreatedAt,
                Roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList(),
                TotalReadSeries = user.ReadingHistories.Select(h => h.SeriesId).Distinct().Count(),
                TotalReadChapters = user.ReadingHistories.Count(),
                TotalCreatedSeries = user.CreatorProfile?.Series.Count ?? 0,
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                WalletBalance = user.Wallet?.CoinBalance ?? 0,
                SubscriptionType = activeSubscription?.VipPlan?.Name ?? "Cơ bản",
                BannerUrl = user.BannerUrl
            };

        }

        public async Task<bool> UpdateProfileAsync(int userId, UpdateProfileDto dto, CancellationToken cancellationToken)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null) return false;

            if (dto.DisplayName != null) user.DisplayName = dto.DisplayName;
            if (dto.Bio != null) user.Bio = dto.Bio;
            if (dto.Avatar != null) user.DisplayAvatar = dto.Avatar;
            if (dto.BannerUrl != null) user.BannerUrl = dto.BannerUrl;


            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<List<ReadingHistoryDto>> GetReadingHistoryAsync(int userId, CancellationToken cancellationToken)
        {
            return await _context.ReadingHistories
                .Where(rh => rh.UserId == userId)
                .OrderByDescending(rh => rh.LastReadAt)
                .Select(rh => new ReadingHistoryDto
                {
                    SeriesId = rh.SeriesId,
                    Title = rh.Series.Title,
                    CoverUrl = rh.Series.CoverImageUrl ?? string.Empty,
                    LastChapterId = rh.LastChapterId,
                    LastChapterTitle = rh.LastChapter.Title ?? $"Chương {rh.LastChapter.ChapterNumber}",
                    LastPageNumber = rh.LastPageNumber,
                    LastReadAt = rh.LastReadAt
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<VipPlanDto>> GetVipPlansAsync(CancellationToken cancellationToken)
        {
            return await _context.VipPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.PriceVnd)
                .Select(p => new VipPlanDto
                {
                    PlanId = p.PlanId,
                    Name = p.Name,
                    Description = p.Description,
                    PriceVnd = p.PriceVnd,
                    DurationDays = p.DurationDays,
                    AutoUnlockChapter = p.AutoUnlockChapter,
                    IsActive = p.IsActive
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<UserSearchDto>> SearchUsersAsync(string query, CancellationToken cancellationToken)
        {
            var usersQuery = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query))
            {
                usersQuery = usersQuery.Where(u => u.Username.Contains(query) || (u.DisplayName != null && u.DisplayName.Contains(query)));
            }

            return await usersQuery
                .Take(20)
                .Select(u => new UserSearchDto
                {
                    UserId = u.UserId,
                    Username = u.Username,
                    DisplayName = u.DisplayName,
                    Avatar = u.DisplayAvatar
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<UserProfileDto?> GetPublicProfileAsync(string username, CancellationToken cancellationToken)
        {
            var user = await _context.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.ReadingHistories)
                .Include(u => u.VipSubscriptions).ThenInclude(vs => vs.VipPlan)
                .Include(u => u.CreatorProfile).ThenInclude(cp => cp.Series)
                .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

            if (user == null) return null;

            var activeSubscription = user.VipSubscriptions
                .Where(vs => vs.Status == SubscriptionStatus.ACTIVE && vs.EndDate > DateTime.UtcNow)
                .OrderByDescending(vs => vs.EndDate)
                .FirstOrDefault();

            var followersCount = user.CreatorProfile != null 
                ? await _context.Follows.CountAsync(f => f.TargetId == user.CreatorProfile.CreatorId && f.TargetType == FollowTargetType.CREATOR, cancellationToken)
                : 0;
            
            var followingCount = await _context.Follows.CountAsync(f => f.UserId == user.UserId, cancellationToken);

            return new UserProfileDto
            {
                UserId = user.UserId,
                Username = user.Username,
                Email = string.Empty, // Hide email for public profile
                DisplayName = user.DisplayName,
                Bio = user.Bio,
                Avatar = user.DisplayAvatar,
                CreatedAt = user.CreatedAt,
                Roles = user.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList(),
                TotalReadSeries = user.ReadingHistories.Select(h => h.SeriesId).Distinct().Count(),
                TotalReadChapters = user.ReadingHistories.Count(),
                TotalCreatedSeries = user.CreatorProfile?.Series.Count ?? 0,
                FollowersCount = followersCount,
                FollowingCount = followingCount,
                WalletBalance = 0, // Hide wallet balance
                SubscriptionType = activeSubscription?.VipPlan?.Name ?? "Cơ bản",
                BannerUrl = user.BannerUrl
            };
        }
        public async Task<UserSettingsDto?> GetUserSettingsAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null) return null;

            return new UserSettingsDto
            {
                NotificationSettings = user.NotificationSettings,
                PrivacySettings = user.PrivacySettings,
                AppearanceSettings = user.AppearanceSettings
            };
        }

        public async Task<bool> UpdateUserSettingsAsync(int userId, UserSettingsDto dto, CancellationToken cancellationToken)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
            if (user == null) return false;

            user.NotificationSettings = dto.NotificationSettings;
            user.PrivacySettings = dto.PrivacySettings;
            user.AppearanceSettings = dto.AppearanceSettings;

            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
