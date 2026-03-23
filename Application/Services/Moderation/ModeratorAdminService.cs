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
      var moderatorRoleId = await GetModeratorRoleId(cancellationToken);

      var query = _context
          .UserRoles.Include(ur => ur.User)
          .Where(ur => ur.RoleId == moderatorRoleId)
          .AsQueryable();

      if (!string.IsNullOrWhiteSpace(request.Keyword))
      {
        var keyword = request.Keyword.Trim();
        query = query.Where(ur =>
            ur.User.Username.Contains(keyword) || ur.User.Email.Contains(keyword)
        );
      }

      if (request.IsActive.HasValue)
      {
        query = query.Where(ur => ur.User.IsActive == request.IsActive.Value);
      }

      var total = await query.CountAsync(cancellationToken);

      var items = await query
          .OrderByDescending(ur => ur.AssignedAt)
          .Skip((request.Page - 1) * request.PageSize)
          .Take(request.PageSize)
          .Select(ur => new ModeratorDto
          {
            UserId = ur.UserId,
            Username = ur.User.Username,
            Email = ur.User.Email,
            DisplayName = ur.User.DisplayName,
            IsActive = ur.User.IsActive,
            AssignedAt = ur.AssignedAt,
          })
          .ToListAsync(cancellationToken);

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

    public async Task RemoveAsync(int userId, CancellationToken cancellationToken = default)
    {
      var moderatorRoleId = await GetModeratorRoleId(cancellationToken);

      var userRole =
          await _context.UserRoles.FirstOrDefaultAsync(
              ur => ur.UserId == userId && ur.RoleId == moderatorRoleId,
              cancellationToken
          )
          ?? throw new KeyNotFoundException(
              "Người dùng không phải moderator hoặc không tồn tại."
          );

      _context.UserRoles.Remove(userRole);
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
