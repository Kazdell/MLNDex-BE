using Application.DTOs.User;
using Application.DTOs.Common;
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
        BannerUrl = user.BannerUrl,
        CannotUpload = user.CannotUpload
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

    public async Task<PagedResult<UserSearchDto>> SearchUsersAsync(
        string query, int page, int pageSize,
        string? roleFilter, string? statusFilter,
        CancellationToken cancellationToken)
    {
      var usersQuery = _context.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .AsQueryable();

      // Text search filter
      if (!string.IsNullOrWhiteSpace(query))
      {
        var q = query.Trim();
        usersQuery = usersQuery.Where(u =>
            u.Username.Contains(q) ||
            (u.DisplayName != null && u.DisplayName.Contains(q)));
      }

      // Role filter
      if (!string.IsNullOrWhiteSpace(roleFilter) && Enum.TryParse<RoleName>(roleFilter, true, out var roleEnum))
      {
        usersQuery = usersQuery.Where(u =>
            u.UserRoles.Any(ur => ur.Role.RoleName == roleEnum));
      }

      // Status filter
      if (!string.IsNullOrWhiteSpace(statusFilter))
      {
        if (statusFilter.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase))
          usersQuery = usersQuery.Where(u => u.IsActive);
        else if (statusFilter.Equals("BANNED", StringComparison.OrdinalIgnoreCase))
          usersQuery = usersQuery.Where(u => !u.IsActive);
      }

      // Count before pagination
      var totalCount = await usersQuery.CountAsync(cancellationToken);

      // Paginate & project
      var items = await usersQuery
          .OrderByDescending(u => u.CreatedAt)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .Select(u => new UserSearchDto
          {
            UserId = u.UserId,
            Username = u.Username,
            DisplayName = u.DisplayName,
            Avatar = u.DisplayAvatar,
            Roles = u.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList(),
            IsActive = u.IsActive,
            Status = u.IsActive ? "ACTIVE" : "BANNED",
            CreatedAt = u.CreatedAt
          })
          .ToListAsync(cancellationToken);

      return new PagedResult<UserSearchDto>(items, totalCount, page, pageSize);
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
        BannerUrl = user.BannerUrl,
        CannotUpload = user.CannotUpload
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

    public async Task<UserStatsDto> GetUserStatsAsync(int days, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var startDateCurrent = now.AddDays(-days);
        var startDate7Days = now.AddDays(-7);

        // Fetch basic counts
        var totalUsers = await _context.Users.CountAsync(cancellationToken);
        var activeUsers = await _context.Users.CountAsync(u => u.IsActive == true, cancellationToken);
        var bannedUsers = await _context.Users.CountAsync(u => u.IsActive == false, cancellationToken);
        var newMembersLast7Days = await _context.Users.CountAsync(u => u.CreatedAt >= startDate7Days, cancellationToken);

        // Chart Data (Group by Date)
        var recentUsers = await _context.Users
            .Where(u => u.CreatedAt >= startDateCurrent)
            .Select(u => new { u.CreatedAt })
            .ToListAsync(cancellationToken);

        // Let's do a better grouping via memory
        var isLongPeriod = days > 31;
        var groupedList = recentUsers
            .GroupBy(u => isLongPeriod ? new DateTime(u.CreatedAt.Year, u.CreatedAt.Month, 1) : u.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new UserChartDataDto
            {
                Date = isLongPeriod ? g.Key.ToString("MM/yyyy") : g.Key.ToString("dd/MM"),
                Count = g.Count()
            })
            .ToList();

        return new UserStatsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            BannedUsers = bannedUsers,
            NewMembersLast7Days = newMembersLast7Days,
            ChartData = groupedList
        };
    }
  }
}
