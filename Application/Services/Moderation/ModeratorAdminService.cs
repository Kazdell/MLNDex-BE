using Application.DTOs.Moderation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
  public class ModeratorAdminService : IModeratorAdminService
  {
    private readonly IMlndexDbContext _context;

    public ModeratorAdminService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<ModeratorListResponse> GetModeratorsAsync(
        ModeratorListRequest request,
        CancellationToken cancellationToken = default
    )
    {
      var adminRole = RoleName.ADMIN.ToString();
      var modRole = RoleName.MODERATOR.ToString();

      // Fetch users who have either MODERATOR or ADMIN role
      var query = _context.Users
          .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
          .Where(u => u.UserRoles.Any(ur => ur.Role.RoleName == RoleName.ADMIN || ur.Role.RoleName == RoleName.MODERATOR))
          .AsQueryable();

      if (!string.IsNullOrWhiteSpace(request.Keyword))
      {
        var keyword = request.Keyword.Trim();
        query = query.Where(u =>
            u.Username.Contains(keyword) || u.Email.Contains(keyword) || (u.DisplayName != null && u.DisplayName.Contains(keyword))
        );
      }

      if (request.IsActive.HasValue)
      {
        query = query.Where(u => u.IsActive == request.IsActive.Value);
      }

      var total = await query.CountAsync(cancellationToken);

      var users = await query
          .OrderByDescending(u => u.CreatedAt) // Or any logic
          .Skip((request.Page - 1) * request.PageSize)
          .Take(request.PageSize)
          .ToListAsync(cancellationToken);

      var items = users.Select(u => new ModeratorDto
      {
        UserId = u.UserId,
        Username = u.Username,
        Email = u.Email,
        DisplayName = u.DisplayName,
        IsActive = u.IsActive,
        AssignedAt = u.UserRoles.Where(ur => ur.Role.RoleName == RoleName.ADMIN || ur.Role.RoleName == RoleName.MODERATOR)
                             .Max(ur => ur.AssignedAt),
        Roles = u.UserRoles.Select(ur => ur.Role.RoleName.ToString()).ToList()
      }).ToList();

      return new ModeratorListResponse
      {
        Items = items,
        TotalCount = total,
        Page = request.Page,
        PageSize = request.PageSize,
      };
    }

    public async Task<ModeratorDto> AssignAsync(
        int userId,
        CancellationToken cancellationToken = default
    )
    {
      var user =
          await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
          ?? throw new KeyNotFoundException($"User {userId} không tồn tại.");

      var moderatorRoleId = await GetModeratorRoleId(cancellationToken);

      var existing = await _context.UserRoles.FirstOrDefaultAsync(
          ur => ur.UserId == userId && ur.RoleId == moderatorRoleId,
          cancellationToken
      );

      if (existing != null)
      {
        return new ModeratorDto
        {
          UserId = user.UserId,
          Username = user.Username,
          Email = user.Email,
          DisplayName = user.DisplayName,
          IsActive = user.IsActive,
          AssignedAt = existing.AssignedAt,
        };
      }

      var now = DateTime.UtcNow;
      _context.UserRoles.Add(
          new UserRole
          {
            UserId = userId,
            RoleId = moderatorRoleId,
            AssignedAt = now,
          }
      );

      await _context.SaveChangesAsync(cancellationToken);

      return new ModeratorDto
      {
        UserId = user.UserId,
        Username = user.Username,
        Email = user.Email,
        DisplayName = user.DisplayName,
        IsActive = user.IsActive,
        AssignedAt = now,
      };
    }

    public async Task RemoveAsync(int userId, int moderatorId, CancellationToken cancellationToken = default)
    {
      if (userId == moderatorId)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Bạn không thể tự gỡ bỏ quyền hạn của chính mình. Hãy nhờ một Admin khác.");

      var user = await _context.Users
          .Include(u => u.UserRoles)
          .ThenInclude(ur => ur.Role)
          .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
          ?? throw new KeyNotFoundException("Người dùng không tồn tại.");

      var staffRoles = user.UserRoles
          .Where(ur => ur.Role.RoleName == RoleName.ADMIN || ur.Role.RoleName == RoleName.MODERATOR)
          .ToList();

      if (!staffRoles.Any())
        throw new KeyNotFoundException("Người dùng không phải moderator hoặc admin.");

      // Security Check: Minimum 1 Admin
      if (staffRoles.Any(ur => ur.Role.RoleName == RoleName.ADMIN))
      {
        var adminRoleId = staffRoles.First(ur => ur.Role.RoleName == RoleName.ADMIN).RoleId;
        var otherAdminsCount = await _context.UserRoles.CountAsync(ur => ur.RoleId == adminRoleId && ur.UserId != userId, cancellationToken);
        if (otherAdminsCount == 0)
        {
          throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Hệ thống phải tồn tại ít nhất một Admin. Bạn không thể gỡ bỏ Admin cuối cùng.");
        }
      }

      _context.UserRoles.RemoveRange(staffRoles);
      await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> GetModeratorRoleId(CancellationToken cancellationToken)
    {
      var role = await _context.Roles.FirstOrDefaultAsync(
          r => r.RoleName == RoleName.MODERATOR,
          cancellationToken
      );
      if (role != null)
        return role.RoleId;

      role = new Role { RoleName = RoleName.MODERATOR };
      _context.Roles.Add(role);
      await _context.SaveChangesAsync(cancellationToken);
      return role.RoleId;
    }
  }
}
