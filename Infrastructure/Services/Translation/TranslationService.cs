using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Mlndex.Data;

namespace Infrastructure.Services.Translation
{
    public class TranslationService : ITranslationService
    {
        private readonly MlndexDbContext _context;

        public TranslationService(MlndexDbContext context)
        {
            _context = context;
        }

        public async Task<TranslationDto> UploadTranslationAsync(int uploaderId, UploadTranslationDto dto)
        {
            // Verify permission
            var permission = await _context.TranslationPermissions
                .Include(p => p.Team)
                .ThenInclude(t => t.TeamMembers)
                .FirstOrDefaultAsync(p => p.PermissionId == dto.PermissionId && p.ChapterId == dto.ChapterId);

            if (permission == null || permission.Status != TranslationPermissionStatus.GRANTED)
            {
                throw new Exception("Translation permission not found or not granted.");
            }

            // Verify uploader is in the team
            if (!permission.Team.TeamMembers.Any(m => m.UserId == uploaderId && m.IsActive))
            {
                throw new Exception("Uploader is not an active member of the translation team.");
            }

            var translation = new Domain.Entities.Translation
            {
                ChapterId = dto.ChapterId,
                PermissionId = dto.PermissionId,
                Language = dto.Language,
                ContentType = dto.ContentType,
                QualityStatus = TranslationQualityStatus.DRAFT,
                ModerationStatus = ModerationStatus.APPROVED // Assuming auto-approve for now
            };

            _context.Translations.Add(translation);
            await _context.SaveChangesAsync();

            // Handle Content
            if (dto.ContentType == ContentType.IMAGE && dto.ImageUrls != null)
            {
                int pageNum = 1;
                foreach (var url in dto.ImageUrls)
                {
                    _context.TranslationPages.Add(new TranslationPage
                    {
                        TranslationId = translation.TranslationId,
                        PageNumber = pageNum++,
                        TranslationImageUrl = url
                    });
                }
            }
            else if (dto.ContentType == ContentType.TEXT && !string.IsNullOrEmpty(dto.ContentUrl))
            {
                _context.TranslationTexts.Add(new TranslationText
                {
                    TranslationId = translation.TranslationId,
                    ContentUrl = dto.ContentUrl,
                    WordCount = dto.WordCount ?? 0
                });
            }

            await _context.SaveChangesAsync();
            return await GetTranslationByIdAsync(translation.TranslationId) ?? throw new Exception("Translation failed to retrieve.");
        }

        public async Task<TranslationDto?> GetTranslationByIdAsync(int translationId)
        {
            var translation = await _context.Translations
                .Include(t => t.TranslationPages)
                .Include(t => t.TranslationText)
                .FirstOrDefaultAsync(t => t.TranslationId == translationId);

            if (translation == null) return null;

            return MapToDto(translation);
        }

        public async Task<IEnumerable<TranslationDto>> GetTranslationsBySeriesAsync(int seriesId)
        {
            var translations = await _context.Translations
                .Include(t => t.Chapter)
                .Where(t => t.Chapter.SeriesId == seriesId)
                .ToListAsync();

            return translations.Select(MapToDto);
        }

        public async Task<IEnumerable<TranslationDto>> GetAllTranslationsAsync()
        {
            var translations = await _context.Translations.ToListAsync();
            return translations.Select(MapToDto);
        }

        public async Task<TranslationDto> EditTranslationAsync(int translationId, int uploaderId, EditTranslationDto dto)
        {
            var translation = await _context.Translations
                .Include(t => t.Permission)
                .ThenInclude(p => p.Team)
                .ThenInclude(t => t.TeamMembers)
                .FirstOrDefaultAsync(t => t.TranslationId == translationId);

            if (translation == null) throw new Exception("Translation not found.");

            if (!translation.Permission.Team.TeamMembers.Any(m => m.UserId == uploaderId && m.IsActive))
            {
                throw new Exception("Unauthorized to edit.");
            }

            if (!string.IsNullOrEmpty(dto.Language))
            {
                translation.Language = dto.Language;
            }

            // In a real scenario, we would update pages/text here based on logic
            // Assuming we just append/replace for simplicity in this implementation plan

            await _context.SaveChangesAsync();
            return await GetTranslationByIdAsync(translationId) ?? throw new Exception("Error retrieving updated translation.");
        }

        public async Task<bool> DeleteTranslationAsync(int translationId, int uploaderId)
        {
            var translation = await _context.Translations
                .Include(t => t.Permission)
                .ThenInclude(p => p.Team)
                .ThenInclude(t => t.TeamMembers)
                .FirstOrDefaultAsync(t => t.TranslationId == translationId);

            if (translation == null) return false;

            if (!translation.Permission.Team.TeamMembers.Any(m => m.UserId == uploaderId && m.IsActive))
            {
                throw new Exception("Unauthorized to delete.");
            }

            _context.Translations.Remove(translation);
            await _context.SaveChangesAsync();
            return true;
        }

        private TranslationDto MapToDto(Domain.Entities.Translation t)
        {
            return new TranslationDto
            {
                TranslationId = t.TranslationId,
                ChapterId = t.ChapterId,
                Language = t.Language,
                ContentType = t.ContentType.ToString(),
                QualityStatus = t.QualityStatus.ToString(),
                ModerationStatus = t.ModerationStatus.ToString(),
                PublishedAt = t.PublishedAt,
                Pages = t.TranslationPages?.OrderBy(p => p.PageNumber).Select(p => p.TranslationImageUrl).ToList(),
                TextContent = t.TranslationText?.ContentUrl
            };
        }
    }
}
