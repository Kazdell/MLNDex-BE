using Application.Interfaces.Common;
using Application.Interfaces.Data;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Application.Interfaces.Notification;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Translation
{
    public class TranslationPermissionService : ITranslationPermissionService
    {
        private readonly IMlndexDbContext _context;
        private readonly IUserContext _userContext;
        private readonly INotificationService _notificationService;

        public TranslationPermissionService(IMlndexDbContext context, IUserContext userContext, INotificationService notificationService)
        {
            _context = context;
            _userContext = userContext;
            _notificationService = notificationService;
        }

        public async Task<TranslationPermissionDto> RequestPermissionAsync(RequestPermissionDto dto)
        {
            var requesterId = _userContext.UserId;
            if (requesterId == null) throw new UnauthorizedAccessException();

            // Verify team leader or translator
            var isMember = await _context.TeamMembers
                .AnyAsync(m => m.TeamId == dto.TeamId && m.UserId == requesterId && m.IsActive);

            if (!isMember)
                throw new Exception("You are not an active member of this translation team.");

            // Verify series exists
            var series = await _context.Series
                .FirstOrDefaultAsync(s => s.SeriesId == dto.SeriesId);

            if (series == null)
                throw new Exception("Series not found.");

            // Tìm UserId của Tác giả từ CreatorId
            var creatorProfile = await _context.CreatorProfiles
                .FirstOrDefaultAsync(c => c.CreatorId == series.CreatorId);

            if (creatorProfile == null)
                throw new Exception("Creator profile not found for this series.");

            // Create permission record
            var permission = new TranslationPermission
            {
                SeriesId = dto.SeriesId,
                TeamId = dto.TeamId,
                GrantedBy = creatorProfile.UserId, // Use UserId here
                Status = TranslationPermissionStatus.PENDING,
                Note = dto.Note
            };

            _context.TranslationPermissions.Add(permission);
            await _context.SaveChangesAsync();

            // Gửi thông báo cho Tác giả bằng SignalR
            var teamName = await _context.TranslationTeams.Where(t => t.TeamId == dto.TeamId).Select(t => t.TeamName).FirstOrDefaultAsync();
            var creatorUserId = await _context.CreatorProfiles
                .Where(c => c.CreatorId == series.CreatorId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            await _notificationService.CreateNotificationAsync(
                creatorUserId,
                "Yêu cầu dịch truyện mới",
                $"Nhóm dịch {teamName ?? "đối tác"} vừa gửi yêu cầu muốn dịch bộ truyện {series.Title} của bạn.",
                "/creator/translation-requests",
                NotificationType.TRANSLATION_REQUEST
            );

            return await MapToDtoAsync(permission);
        }

        public async Task<TranslationPermissionDto> ReviewPermissionAsync(int permissionId, ReviewPermissionDto dto)
        {
            var creatorId = _userContext.UserId;
            if (creatorId == null) throw new UnauthorizedAccessException();

            var permission = await _context.TranslationPermissions
                .Include(p => p.Series)
                .FirstOrDefaultAsync(p => p.PermissionId == permissionId);

            if (permission == null)
                throw new Exception("Permission request not found.");

            // Ensure the person reviewing is the owner of the creator profile associated with the series
            var seriesCreatorUserId = await _context.CreatorProfiles
                .Where(c => c.CreatorId == permission.Series.CreatorId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();

            if (seriesCreatorUserId != creatorId)
                throw new Exception("Unauthorized. Only the creator of the series can review this translation request.");

            if (dto.IsApproved)
            {
                permission.Status = TranslationPermissionStatus.GRANTED;
                permission.GrantedAt = DateTime.UtcNow;
            }
            else
            {
                permission.Status = TranslationPermissionStatus.DENIED;
                permission.RevokedAt = DateTime.UtcNow;
            }

            // Fix legacy records that might have wrong GrantedBy ID
            permission.GrantedBy = seriesCreatorUserId;

            await _context.SaveChangesAsync();

            // Gửi thông báo cho toàn bộ thành viên của nhóm dịch
            var teamMembers = await _context.TeamMembers
                .Where(m => m.TeamId == permission.TeamId && m.IsActive)
                .Select(m => m.UserId)
                .ToListAsync();

            var resultTitle = dto.IsApproved ? "Yêu cầu dịch truyện được chấp thuận" : "Yêu cầu dịch truyện bị từ chối";
            var resultMessage = dto.IsApproved 
                ? $"Tác giả bộ truyện {permission.Series.Title} đã chấp thuận yêu cầu dịch của nhóm bạn. Bạn đã có thể bắt đầu đăng chương mới."
                : $"Tác giả bộ truyện {permission.Series.Title} đã từ chối yêu cầu dịch của nhóm bạn.";
            var link = $"/translation/sent-requests/{permission.TeamId}"; // Trang yêu cầu gửi đi của nhóm

            foreach (var memberId in teamMembers)
            {
                await _notificationService.CreateNotificationAsync(
                    memberId,
                    resultTitle,
                    resultMessage,
                    link,
                    dto.IsApproved ? NotificationType.TRANSLATION_GRANTED : NotificationType.TRANSLATION_REVOKED
                );
            }

            return await MapToDtoAsync(permission);
        }

        public async Task<IEnumerable<TranslationPermissionDto>> GetTeamPermissionsAsync(int teamId)
        {
            var permissions = await _context.TranslationPermissions
                .Where(p => p.TeamId == teamId)
                .OrderByDescending(p => p.PermissionId)
                .ToListAsync();

            var dtos = new List<TranslationPermissionDto>();
            foreach (var p in permissions)
            {
                dtos.Add(await MapToDtoAsync(p));
            }
            return dtos;
        }

        public async Task<IEnumerable<TranslationPermissionDto>> GetCreatorPermissionsAsync(int userId)
        {
            var creatorId = await _context.CreatorProfiles
                .Where(c => c.UserId == userId)
                .Select(c => c.CreatorId)
                .FirstOrDefaultAsync();

            if (creatorId == 0) return new List<TranslationPermissionDto>();

            var permissions = await _context.TranslationPermissions
                .Where(p => p.GrantedBy == userId || p.GrantedBy == creatorId)
                .OrderByDescending(p => p.PermissionId)
                .ToListAsync();

            var dtos = new List<TranslationPermissionDto>();
            foreach (var p in permissions)
            {
                dtos.Add(await MapToDtoAsync(p));
            }
            return dtos;
        }

        private async Task<TranslationPermissionDto> MapToDtoAsync(TranslationPermission p)
        {
            var seriesTitle = await _context.Series.Where(s => s.SeriesId == p.SeriesId).Select(s => s.Title).FirstOrDefaultAsync();
            var teamName = await _context.TranslationTeams.Where(t => t.TeamId == p.TeamId).Select(t => t.TeamName).FirstOrDefaultAsync();

            return new TranslationPermissionDto
            {
                PermissionId = p.PermissionId,
                SeriesId = p.SeriesId,
                SeriesTitle = seriesTitle,
                TeamId = p.TeamId,
                TeamName = teamName,
                GrantedBy = p.GrantedBy,
                Status = p.Status.ToString(),
                RequestedAt = p.CreatedAt,
                GrantedAt = p.GrantedAt,
                RevokedAt = p.RevokedAt,
                Note = p.Note
            };
        }
    }
}
