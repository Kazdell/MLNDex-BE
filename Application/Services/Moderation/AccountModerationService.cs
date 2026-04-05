using Application.DTOs.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
  public class AccountModerationService : IAccountModerationService
  {
    private readonly IMlndexDbContext _context;

    public AccountModerationService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<AccountActionResponse> ApplyAsync(
        int userId,
        int moderatorId,
        AccountActionRequest request,
        CancellationToken cancellationToken = default
    )
    {
      var user =
          await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
          ?? throw new KeyNotFoundException("User không tồn tại.");

      if (userId == moderatorId && request.Action == AccountActionType.DEACTIVATE)
        throw new InvalidOperationException("Bạn không thể tự vô hiệu hóa tài khoản của chính mình.");

      // Fetch the moderator to check their permissions
      var moderator = await _context.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(u => u.UserId == moderatorId, cancellationToken);

      if (moderator == null) throw new UnauthorizedAccessException("Người thực hiện không hợp lệ.");

      var modRoles = moderator.UserRoles.Select(ur => ur.Role.RoleName).ToList();
      var isModeratorLevel = modRoles.Contains(RoleName.MODERATOR) && !modRoles.Contains(RoleName.ADMIN);
      var isAdminLevel = modRoles.Contains(RoleName.ADMIN);

      var currentUserRolesCount = await _context.UserRoles
          .Include(ur => ur.Role)
          .Where(ur => ur.UserId == userId)
          .Select(ur => ur.Role.RoleName)
          .ToListAsync(cancellationToken);

      // Role Hierarchy Logic for Actions
      if (isModeratorLevel)
      {
        if (currentUserRolesCount.Contains(RoleName.ADMIN) || currentUserRolesCount.Contains(RoleName.MODERATOR))
          throw new UnauthorizedAccessException("Bạn không có quyền thực thi hành động này lên Hệ thống Quản trị (Moderator/Admin).");
      }
      else if (isAdminLevel)
      {
        // Admin can act on other Admins, but not themselves (for deactivation)
        if (userId == moderatorId && request.Action == AccountActionType.DEACTIVATE)
          throw new InvalidOperationException("Bạn không thể tự vô hiệu hóa tài khoản của chính mình.");

        // If deactivating another Admin, ensure at least one OTHER active Admin remains
        if (request.Action == AccountActionType.DEACTIVATE && currentUserRolesCount.Contains(RoleName.ADMIN))
        {
          var activeAdminsCount = await _context.UserRoles
              .Include(ur => ur.Role)
              .Include(ur => ur.User)
              .CountAsync(ur => ur.Role.RoleName == RoleName.ADMIN && ur.User.IsActive && ur.UserId != userId, cancellationToken);

          if (activeAdminsCount == 0)
          {
            throw new InvalidOperationException("Không thể vô hiệu hóa Admin này vì đây là Admin hoạt động cuối cùng của hệ thống.");
          }
        }
      }

      switch (request.Action)
      {
        case AccountActionType.WARN:
          await AddNotificationAsync(
              userId,
              "Cảnh báo tài khoản",
              request.Reason ?? "Vi phạm chính sách.",
              cancellationToken
          );
          break;
        case AccountActionType.DEACTIVATE:
          user.IsActive = false;
          await AddNotificationAsync(
              userId,
              "Tài khoản bị vô hiệu hóa",
              request.Reason ?? "Vi phạm chính sách.",
              cancellationToken
          );
          break;
        case AccountActionType.ACTIVATE:
          user.IsActive = true;
          await AddNotificationAsync(
              userId,
              "Tài khoản được kích hoạt lại",
              request.Reason ?? "",
              cancellationToken
          );
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }

      await _context.SaveChangesAsync(cancellationToken);

      return new AccountActionResponse
      {
        UserId = user.UserId,
        IsActive = user.IsActive,
        Message = request.Action switch
        {
          AccountActionType.WARN => "Đã gửi cảnh báo",
          AccountActionType.DEACTIVATE => "Đã vô hiệu hóa tài khoản",
          AccountActionType.ACTIVATE => "Đã kích hoạt tài khoản",
          _ => "",
        },
      };
    }

    private async Task AddNotificationAsync(
        int userId,
        string title,
        string message,
        CancellationToken cancellationToken
    )
    {
      _context.Notifications.Add(
          new Notification
          {
            UserId = userId,
            NotificationType = NotificationType.SYSTEM,
            Title = title,
            Message = message,
            ActionUrl = string.Empty,
            RelatedEntityId = null,
            RelatedEntityType = null,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
          }
      );
      await Task.CompletedTask;
    }

    public async Task<bool> UpdateRolesAsync(
        int userId,
        int moderatorId,
        UpdateUserRolesRequest request,
        CancellationToken cancellationToken = default
    )
    {
      var user = await _context.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

      if (user == null) return false;

      // Fetch the moderator to check their permissions
      var moderator = await _context.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(u => u.UserId == moderatorId, cancellationToken);

      if (moderator == null) throw new UnauthorizedAccessException("Người thực hiện không hợp lệ.");

      var modRoles = moderator.UserRoles.Select(ur => ur.Role.RoleName).ToList();
      var isModeratorLevel = modRoles.Contains(RoleName.MODERATOR) && !modRoles.Contains(RoleName.ADMIN);
      var isAdminLevel = modRoles.Contains(RoleName.ADMIN);

      // Security Check: Minimum 1 Admin
      var currentRoles = user.UserRoles.Select(ur => ur.Role.RoleName).ToList();
      var newRoles = request.Roles.Select(r => Enum.TryParse<RoleName>(r.ToUpper(), out var rn) ? rn : (RoleName?)null)
                                  .Where(rn => rn != null)
                                  .Select(rn => rn!.Value)
                                  .ToList();

      if (currentRoles.Contains(RoleName.ADMIN) && !newRoles.Contains(RoleName.ADMIN))
      {
        var adminRoleId = (await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == RoleName.ADMIN, cancellationToken))?.RoleId;
        var otherAdminsCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId && ur.UserId != userId, cancellationToken);

        if (otherAdminsCount == 0)
        {
          throw new InvalidOperationException("Hệ thống phải tồn tại ít nhất một Admin. Bạn không thể gỡ bỏ quyền Admin cuối cùng.");
        }
      }

      // Role Hierarchy Logic
      if (isModeratorLevel)
      {
        if (currentRoles.Contains(RoleName.ADMIN) || currentRoles.Contains(RoleName.MODERATOR))
          throw new UnauthorizedAccessException("Bạn không có quyền thay đổi vai trò của Hệ thống Quản trị (Moderator/Admin).");
        if (newRoles.Contains(RoleName.ADMIN) || newRoles.Contains(RoleName.MODERATOR))
          throw new UnauthorizedAccessException("Bạn không có quyền cấp quyền Hệ thống Quản trị (Moderator/Admin).");
      }
      // Admin level can edit anyone's roles (including other Admins)
      // Safety checks for minimum admin count were handled above

      // Remove existing roles
      _context.UserRoles.RemoveRange(user.UserRoles);

      // Add new roles
      foreach (var roleStr in request.Roles)
      {
        if (Enum.TryParse<RoleName>(roleStr.ToUpper(), out var roleNameEnum))
        {
          var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleName == roleNameEnum, cancellationToken);
          if (role != null)
          {
            user.UserRoles.Add(new UserRole
            {
              UserId = userId,
              RoleId = role.RoleId,
              AssignedAt = DateTime.UtcNow
            });
          }
        }
      }

      await _context.SaveChangesAsync(cancellationToken);
      return true;
    }
  }
}
