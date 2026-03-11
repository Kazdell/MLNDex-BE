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

            // Create permission record
            var permission = new TranslationPermission
            {
                SeriesId = dto.SeriesId,
                TeamId = dto.TeamId,
                GrantedBy = series.CreatorId, // Creator gives permission
                Status = TranslationPermissionStatus.PENDING,
                Note = dto.Note
            };

            _context.TranslationPermissions.Add(permission);
            await _context.SaveChangesAsync();

            // Gửi thông báo cho Tác giả bằng SignalR
            var teamName = await _context.TranslationTeams.Where(t => t.TeamId == dto.TeamId).Select(t => t.TeamName).FirstOrDefaultAsync();
            await _notificationService.CreateNotificationAsync(
                series.CreatorId,
                "Yêu cầu dịch truyện mới",
                $"Nhóm dịch {teamName ?? "đối tác"} vừa gửi yêu cầu muốn dịch bộ truyện {series.Title} của bạn.",
                $"/creator/series/{series.SeriesId}/translation-requests",
                NotificationType.TRANSLATION_REQUEST
            );

            return MapToDto(permission);
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

            // Ensure the person reviewing is the creator of the series
            if (permission.Series.CreatorId != creatorId)
                throw new Exception("Unauthorized. Only the creator of the series can review this translation request.");

            if (dto.IsApproved)
            {
                permission.Status = TranslationPermissionStatus.GRANTED;
                permission.GrantedAt = DateTime.UtcNow;
            }
            else
            {
                permission.Status = TranslationPermissionStatus.REVOKED;
                permission.RevokedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return MapToDto(permission);
        }

        private TranslationPermissionDto MapToDto(TranslationPermission p)
        {
            return new TranslationPermissionDto
            {
                PermissionId = p.PermissionId,
                SeriesId = p.SeriesId,
                TeamId = p.TeamId,
                GrantedBy = p.GrantedBy,
                Status = p.Status.ToString(),
                GrantedAt = p.GrantedAt,
                RevokedAt = p.RevokedAt,
                Note = p.Note
            };
        }
    }
}
