using Application.DTOs.Creator;
using Application.Interfaces.Auth;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Services.Creator
{
    public class CreatorService : ICreatorService
    {
        private readonly IMlndexDbContext _db;
        private readonly ITokenService _tokenService;
        private readonly IConfiguration _configuration;

        public CreatorService(
        IMlndexDbContext db,
        ITokenService tokenService,
        IConfiguration configuration)
        {
            _db = db;
            _tokenService = tokenService;
            _configuration = configuration;
        }

        public async Task<CreatorRegisterResponseDto> RegisterAsync(int userId, CreatorRegisterDto dto, CancellationToken ct = default)
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

            // 3. Tạo CreatorProfile
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

            // 4. Gán role Creator
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
                    AssignedAt = DateTime.UtcNow,
                });
            }

            await _db.SaveChangesAsync(ct);

            // 5. Load lại user kèm đầy đủ roles để generate token mới
            var user = await _db.Users
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .FirstAsync(u => u.UserId == userId, ct);

            // 6. Generate token mới có role CREATOR
            var newAccessToken = _tokenService.GenerateJwtToken(user);
            var newRefreshToken = _tokenService.GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(
                Convert.ToDouble(_configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7"));
            await _db.SaveChangesAsync(ct);

            return new CreatorRegisterResponseDto
            {
                CreatorId = profile.CreatorId,
                PenName = profile.PenName,
                ModerationStatus = profile.ModerationStatus.ToString(),
                IsActive = profile.IsActive,
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
            };
        }

        public async Task<UpdateUnlockSettingsDto> GetUnlockSettingsAsync(int userId, CancellationToken ct = default)
        {
            // Note: Changed _context to _db to match your constructor
            var settings = await _db.CreatorProfiles
                .Where(p => p.UserId == userId)
                .Select(p => new UpdateUnlockSettingsDto
                {
                    UnlockEnabled = p.UnlockEnabled,
                    DefaultUnlockPriceCoins = p.DefaultUnlockPriceCoins,
                    FreeAfterEnabled = p.FreeAfterEnabled,
                    DefaultFreeAfterDays = p.DefaultFreeAfterDays
                })
                .FirstOrDefaultAsync(ct);

            if (settings == null)
            {
                // You might want to use your custom Exception type here
                throw new KeyNotFoundException($"Creator profile for User {userId} not found.");
            }

            return settings;
        }

        public async Task<bool> UpdateUnlockSettingsAsync(int userId, UpdateUnlockSettingsDto dto, CancellationToken ct = default)
        {
            var profile = await _db.CreatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId, ct);

            if (profile == null) return false;

            // Mapping the DTO to the Entity
            profile.UnlockEnabled = dto.UnlockEnabled;

            // Logic: If disabled, we null out the values to keep the DB clean
            profile.DefaultUnlockPriceCoins = dto.UnlockEnabled ? dto.DefaultUnlockPriceCoins : null;

            profile.FreeAfterEnabled = dto.FreeAfterEnabled;
            profile.DefaultFreeAfterDays = dto.FreeAfterEnabled ? dto.DefaultFreeAfterDays : null;

            // In EF Core, calling SaveChangesAsync returns the number of state entries written to the DB
            return await _db.SaveChangesAsync(ct) > 0;
        }
    }
}
