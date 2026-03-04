using Application.Interfaces.Data;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Translation
{
    public class TranslationPermissionService : ITranslationPermissionService
    {
        private readonly IMlndexDbContext _context;

        public TranslationPermissionService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<TranslationPermissionDto> RequestPermissionAsync(int requesterId, RequestPermissionDto dto)
        {
            // Verify team leader or translator
            var isMember = await _context.TeamMembers
                .AnyAsync(m => m.TeamId == dto.TeamId && m.UserId == requesterId && m.IsActive);

            if (!isMember)
                throw new Exception("You are not an active member of this translation team.");

            // Verify chapter exists and get creator
            var chapter = await _context.Chapters
                .Include(c => c.Series)
                .FirstOrDefaultAsync(c => c.ChapterId == dto.ChapterId);

            if (chapter == null)
                throw new Exception("Chapter not found.");

            // Create permission record
            var permission = new TranslationPermission
            {
                ChapterId = dto.ChapterId,
                TeamId = dto.TeamId,
                GrantedBy = chapter.Series.CreatorId, // Creator gives permission
                Status = TranslationPermissionStatus.PENDING,
                Note = dto.Note
            };

            _context.TranslationPermissions.Add(permission);
            await _context.SaveChangesAsync();

            // In real app: Fire a Notification to the Creator that a Team has requested permission.

            return MapToDto(permission);
        }

        public async Task<TranslationPermissionDto> ReviewPermissionAsync(int permissionId, int creatorId, ReviewPermissionDto dto)
        {
            var permission = await _context.TranslationPermissions
                .Include(p => p.Chapter)
                .ThenInclude(c => c.Series)
                .FirstOrDefaultAsync(p => p.PermissionId == permissionId);

            if (permission == null)
                throw new Exception("Permission request not found.");

            // Ensure the person reviewing is the creator of the series
            if (permission.Chapter.Series.CreatorId != creatorId)
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

            // In real app: Fire a Notification to the Team leader that their request was approved/rejected.

            return MapToDto(permission);
        }

        private TranslationPermissionDto MapToDto(TranslationPermission p)
        {
            return new TranslationPermissionDto
            {
                PermissionId = p.PermissionId,
                ChapterId = p.ChapterId,
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
