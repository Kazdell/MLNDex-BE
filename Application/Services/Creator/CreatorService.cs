using Application.DTOs.Creator;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Creator
{
    public class CreatorService : ICreatorService
    {
        private readonly IMlndexDbContext _db;

        public CreatorService(IMlndexDbContext db)
        {
            _db = db;
        }

        public async Task<CreatorProfileDto> RegisterAsync(int userId, CreatorRegisterDto dto, CancellationToken ct = default)
        {
            // 1. Kiểm tra đã là creator chưa
            var existing = await _db.CreatorProfiles
                .FirstOrDefaultAsync(c => c.UserId == userId, ct);

            if (existing != null)
                throw new InvalidOperationException("Người dùng đã có hồ sơ nhà sáng tạo.");

            // 2. Kiểm tra bút danh trùng
            var penNameTaken = await _db.CreatorProfiles
                .AnyAsync(c => c.PenName == dto.PenName.Trim(), ct);

            if (penNameTaken)
                throw new InvalidOperationException("Bút danh này đã được sử dụng. Vui lòng chọn tên khác.");

            // 3. Tạo CreatorProfile với APPROVED + IsActive = true (không qua PENDING)
            var profile = new CreatorProfile
            {
                UserId = userId,
                PenName = dto.PenName.Trim(),
                ModerationStatus = ModerationStatus.APPROVED,
                IsActive = true,
                ReputationScore = 0,
                TotalRevenue = 0,
                HideRevenue = false,
            };

            _db.CreatorProfiles.Add(profile);

            // 4. Gán role Creator cho user
            var creatorRole = await _db.Roles
                .FirstOrDefaultAsync(r => r.RoleName == RoleName.CREATOR, ct);

            if (creatorRole == null)
                throw new InvalidOperationException("Role 'Creator' không tồn tại trong hệ thống.");

            var alreadyHasRole = await _db.UserRoles
                .AnyAsync(ur => ur.UserId == userId && ur.RoleId == creatorRole.RoleId, ct);

            if (!alreadyHasRole)
            {
                _db.UserRoles.Add(new UserRole
                {
                    UserId = userId,
                    RoleId = creatorRole.RoleId,
                });
            }

            await _db.SaveChangesAsync(ct);

            return new CreatorProfileDto
            {
                CreatorId = profile.CreatorId,
                PenName = profile.PenName,
                ModerationStatus = profile.ModerationStatus.ToString(),
                IsActive = profile.IsActive,
            };
        }
    }
}
