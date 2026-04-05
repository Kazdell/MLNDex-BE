using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Application.Interfaces.Common;
using Application.Interfaces.Data;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;
using Application.Interfaces.Translation;
using Application.Interfaces.Notification;
using Domain.Entities;
using Application.Interfaces.AIModeration;

namespace Application.Services.Translation
{
  public class TranslationService : ITranslationService
  {
    private readonly IMlndexDbContext _context;
    private readonly IUserContext _userContext;
    private readonly Application.Interfaces.Creator.IStorageService _storage;
    private readonly Microsoft.Extensions.Logging.ILogger<TranslationService> _logger;
    private readonly INotificationService _notificationService;
    private readonly IModerationService _moderationService;

    public TranslationService(
        IMlndexDbContext context,
        IUserContext userContext,
        Application.Interfaces.Creator.IStorageService storage,
        Microsoft.Extensions.Logging.ILogger<TranslationService> logger,
        INotificationService notificationService,
        IModerationService moderationService)
    {
      _context = context;
      _userContext = userContext;
      _storage = storage;
      _logger = logger;
      _notificationService = notificationService;
      _moderationService = moderationService;
    }

    public async Task<TranslationResponse> UploadTranslationAsync(UploadTranslationRequest dto)
    {
      var uploaderId = _userContext.UserId;
      if (uploaderId == null) throw new UnauthorizedAccessException();

      bool isOfficial = false;
      int resolvedTeamId;
      Chapter chapter; // Hoisted for notification access below

      // ── OFFICIAL PATH: PermissionId provided ──
      if (dto.PermissionId != null)
      {
        var permission = await _context.TranslationPermissions
            .Include(p => p.Team)
            .ThenInclude(t => t.TeamMembers)
            .FirstOrDefaultAsync(p => p.PermissionId == dto.PermissionId);

        if (permission == null)
          throw new Exception("Translation permission record not found.");

        if (permission.LanguageId != dto.LanguageId)
          throw new Exception("Language mismatch with the requested permission language.");

        // Verify the chapter belongs to the series for which permission was granted
        chapter = await _context.Chapters
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.ChapterId == dto.ChapterId)
            ?? throw new Exception("Chapter not found.");
        if (chapter.SeriesId != permission.SeriesId) throw new Exception("Permission not valid for this series.");

        // Verify uploader is in the team or is the leader
        bool isUploaderValid = permission.Team.LeaderId == uploaderId || 
                               permission.Team.TeamMembers.Any(m => m.UserId == uploaderId && m.IsActive);
        if (!isUploaderValid)
          throw new Exception("Uploader is not an active member or leader of the translation team.");

        isOfficial = permission.Status == TranslationPermissionStatus.GRANTED;
        resolvedTeamId = permission.TeamId;
      }
      // ── UNOFFICIAL PATH: No permission, TeamId required ──
      else
      {
        if (dto.TeamId == null)
          throw new Exception("TeamId is required for unofficial translations.");

        resolvedTeamId = dto.TeamId.Value;

        // Verify team exists and uploader is an active member
        var team = await _context.TranslationTeams
            .Include(t => t.TeamMembers)
            .FirstOrDefaultAsync(t => t.TeamId == resolvedTeamId);

        if (team == null)
          throw new Exception("Translation team not found.");

        bool isUploaderValid = team.LeaderId == uploaderId || 
                               team.TeamMembers.Any(m => m.UserId == uploaderId && m.IsActive);
        if (!isUploaderValid)
          throw new Exception("Uploader is not an active member or leader of the translation team.");

        // Verify chapter exists
        chapter = await _context.Chapters
            .Include(c => c.Series)
            .FirstOrDefaultAsync(c => c.ChapterId == dto.ChapterId)
            ?? throw new Exception("Chapter not found.");

        // NOTE: Lock check (COIN_LOCK / TIMED_LOCK) will be added here
        // once the Creator team implements chapter locking feature.

        isOfficial = false;

        // ---- AUTO-RESOLVE OR CREATE UNOFFICIAL PERMISSION ----
        var existingPerm = await _context.TranslationPermissions
            .FirstOrDefaultAsync(p => p.TeamId == resolvedTeamId && p.SeriesId == chapter.SeriesId && p.LanguageId == dto.LanguageId);
            
        if (existingPerm != null)
        {
            dto.PermissionId = existingPerm.PermissionId;
        }
        else
        {
            var creatorUserId = await _context.CreatorProfiles
                .Where(c => c.CreatorId == chapter.Series.CreatorId)
                .Select(c => c.UserId)
                .FirstOrDefaultAsync();
                
            var newPerm = new TranslationPermission
            {
               SeriesId = chapter.SeriesId,
               TeamId = resolvedTeamId,
               LanguageId = dto.LanguageId,
               Origin = PermissionOrigin.REQUESTED_BY_TEAM,
               GrantedBy = creatorUserId,
               Status = TranslationPermissionStatus.UNOFFICIAL,
               GrantedAt = null
            };
            _context.TranslationPermissions.Add(newPerm);
            await _context.SaveChangesAsync();
            dto.PermissionId = newPerm.PermissionId;
        }
      }

      var translation = new Domain.Entities.Translation
      {
        ChapterId = dto.ChapterId,
        PermissionId = dto.PermissionId, // always set — populated by both official and unofficial paths above
        TeamId = resolvedTeamId,
        LanguageId = dto.LanguageId,
        ContentType = dto.ContentType,
        QualityStatus = TranslationQualityStatus.DRAFT,
        ModerationStatus = ModerationStatus.PENDING,
        IsOfficial = isOfficial
      };

      if (dto.Credits != null && dto.Credits.Any())
      {
        foreach (var credit in dto.Credits)
        {
          translation.TranslationCredits.Add(new TranslationCredit
          {
            UserId = credit.UserId,
            Role = credit.Role
          });
        }
      }

      if (dto.JointTeamIds != null && dto.JointTeamIds.Any())
      {
        foreach (var teamId in dto.JointTeamIds)
        {
          translation.TeamJoins.Add(new TranslationTeamJoin
          {
            TeamId = teamId
          });
        }
      }

      var uploadedUrls = new List<string>();
      try
      {
        _context.Translations.Add(translation);
        await _context.SaveChangesAsync();

        // Handle Content
        if (dto.ContentType == ContentType.IMAGE && dto.Pages != null && dto.Pages.Count > 0)
        {
          var pageFolder = $"translations/{translation.TranslationId}/pages";
          var semaphore = new SemaphoreSlim(4);

          var uploadTasks = dto.Pages.Select(async (page, index) =>
          {
            await semaphore.WaitAsync();
            try
            {
              var url = await _storage.UploadAsync(
                            page.FileStream,
                            page.FileName,
                            pageFolder,
                            CancellationToken.None);
              return (url, index);
            }
            finally
            {
              semaphore.Release();
            }
          });

          var results = await Task.WhenAll(uploadTasks);

          foreach (var (url, index) in results.OrderBy(r => r.index))
          {
            uploadedUrls.Add(url);
            _context.TranslationPages.Add(new TranslationPage
            {
              TranslationId = translation.TranslationId,
              PageNumber = index + 1,
              TranslationImageUrl = url
            });
          }
        }
        else if (dto.ContentType == ContentType.TEXT && !string.IsNullOrEmpty(dto.ContentText))
        {
          // Create a stream from the text content and upload it as a txt file
          var byteData = Encoding.UTF8.GetBytes(dto.ContentText);
          using var stream = new MemoryStream(byteData);
          var textFolder = $"translations/{translation.TranslationId}/text";
          var fileName = $"content_{Guid.NewGuid()}.txt";

          var url = await _storage.UploadAsync(stream, fileName, textFolder, CancellationToken.None);
          uploadedUrls.Add(url);

          var wordCount = dto.ContentText.Split(new char[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

          _context.TranslationTexts.Add(new TranslationText
          {
            TranslationId = translation.TranslationId,
            ContentUrl = url,
            WordCount = wordCount
          });
        }

        await _context.SaveChangesAsync();
      }
      catch (Exception ex)
      {
        if (uploadedUrls.Count > 0)
        {
          _logger.LogWarning(ex, "Lưu DB thất bại. Đang xóa {Count} tệp đã upload.", uploadedUrls.Count);
          foreach (var url in uploadedUrls)
            await _storage.DeleteAsync(url);
        }
        
        // Rollback DB Entity
        try 
        {
           _context.Translations.Remove(translation);
           await _context.SaveChangesAsync();
        } 
        catch (Exception rollbackEx)
        {
           _logger.LogError(rollbackEx, "Failed to rollback translation record {Id}", translation.TranslationId);
        }
        
        throw;
      }

      // Notify the uploader that their translation is now in the moderation queue
      try
      {
        var translationResult = await GetTranslationByIdAsync(translation.TranslationId)
            ?? throw new Exception("Translation failed to retrieve.");

        var seriesTitle = chapter.Series?.Title ?? "series";

        await _moderationService.EnqueueTranslationForModerationAsync(translation.TranslationId);

        await _notificationService.CreateNotificationAsync(
            userId: uploaderId.Value,
            title: "Đã đưa vào hàng đợi kiểm duyệt!",
            message: $"Bản dịch chương {chapter.ChapterNumber} của {seriesTitle} đã tải lên thành công và đang chờ AI kiểm duyệt.",
            actionUrl: $"/creator/moderation-result",
            type: Domain.Entities.NotificationType.SYSTEM);

        return translationResult;
      }
      catch (Exception notifEx)
      {
        // Notification failure should NOT roll back the successful translation upload
        _logger.LogWarning(notifEx, "[TranslationService] Gửi thông báo thất bại sau upload (TranslationId={TranslationId}).", translation.TranslationId);
        return await GetTranslationByIdAsync(translation.TranslationId) ?? throw new Exception("Translation failed to retrieve.");
      }
    } // end UploadTranslationAsync

    public async Task<TranslationResponse?> GetTranslationByIdAsync(int translationId)
    {
      var translation = await _context.Translations
          .Include(t => t.Chapter)
              .ThenInclude(c => c.Series)
          .Include(t => t.Language)
          .Include(t => t.Team)
          .Include(t => t.Permission)
              .ThenInclude(p => p.Team)
          .Include(t => t.TeamJoins)
              .ThenInclude(tj => tj.Team)
          .Include(t => t.TranslationPages)
          .Include(t => t.TranslationText)
          .FirstOrDefaultAsync(t => t.TranslationId == translationId);

      if (translation == null) return null;

      return MapToDto(translation);
    }

    public async Task<IEnumerable<TranslationResponse>> GetTranslationsBySeriesAsync(int seriesId)
    {
      var translations = await _context.Translations
          .Include(t => t.Language)
          .Include(t => t.Team)
          .Include(t => t.Permission)
              .ThenInclude(p => p.Team)
          .Include(t => t.TeamJoins)
              .ThenInclude(tj => tj.Team)
          .Include(t => t.Chapter)
          .Where(t => t.Chapter.SeriesId == seriesId)
          .ToListAsync();

      return translations.Select(MapToDto);
    }

    public async Task<IEnumerable<TranslationResponse>> GetAllTranslationsAsync()
    {
      var translations = await _context.Translations
          .Include(t => t.Language)
          .Include(t => t.Permission)
              .ThenInclude(p => p.Team)
          .ToListAsync();
      return translations.Select(MapToDto);
    }

    public async Task<TranslationResponse> EditTranslationAsync(int translationId, EditTranslationRequest dto)
    {
      var uploaderId = _userContext.UserId;
      if (uploaderId == null) throw new UnauthorizedAccessException();

      var translation = await _context.Translations
          .Include(t => t.Permission)
          .ThenInclude(p => p.Team)
          .ThenInclude(t => t.TeamMembers)
          .FirstOrDefaultAsync(t => t.TranslationId == translationId);

      if (translation == null) throw new Exception("Translation not found.");
      
      if (translation.Permission == null) throw new Exception("Data consistency error: Missing TranslationPermission.");

      bool isUploaderValid = translation.Permission.Team.LeaderId == uploaderId || 
                             translation.Permission.Team.TeamMembers.Any(m => m.UserId == uploaderId && m.IsActive);
      if (!isUploaderValid)
      {
        throw new Exception("Unauthorized to edit.");
      }

      if (dto.LanguageId > 0)
      {
        translation.LanguageId = dto.LanguageId;
      }

      await _context.SaveChangesAsync();
      return await GetTranslationByIdAsync(translationId) ?? throw new Exception("Error retrieving updated translation.");
    }

    public async Task<bool> DeleteTranslationAsync(int translationId)
    {
      var uploaderId = _userContext.UserId;
      if (uploaderId == null) throw new UnauthorizedAccessException();

      var translation = await _context.Translations
          .Include(t => t.Permission)
          .ThenInclude(p => p.Team)
          .ThenInclude(t => t.TeamMembers)
          .FirstOrDefaultAsync(t => t.TranslationId == translationId);

      if (translation == null) return false;
      
      if (translation.Permission == null) throw new Exception("Data consistency error: Missing TranslationPermission.");

      bool isUploaderValid = translation.Permission.Team.LeaderId == uploaderId || 
                             translation.Permission.Team.TeamMembers.Any(m => m.UserId == uploaderId && m.IsActive);
      if (!isUploaderValid)
      {
        throw new Exception("Unauthorized to delete.");
      }

      var urlsToDelete = new List<string>();

      var pages = await _context.TranslationPages.Where(p => p.TranslationId == translationId).ToListAsync();
      urlsToDelete.AddRange(pages.Select(p => p.TranslationImageUrl));

      var text = await _context.TranslationTexts.Where(t => t.TranslationId == translationId).FirstOrDefaultAsync();
      if (text != null && !string.IsNullOrEmpty(text.ContentUrl))
      {
        urlsToDelete.Add(text.ContentUrl);
      }

      _context.Translations.Remove(translation);
      await _context.SaveChangesAsync();

      // Delete physical files from Storage Service
      var semaphore = new SemaphoreSlim(5);
      var deleteTasks = urlsToDelete.Where(url => !string.IsNullOrEmpty(url)).Select(async url =>
      {
          await semaphore.WaitAsync();
          try
          {
              await _storage.DeleteAsync(url);
          }
          catch (Exception ex)
          {
              _logger.LogWarning(ex, "Failed to delete storage file {Url} for translation {TranslationId}", url, translationId);
          }
          finally
          {
              semaphore.Release();
          }
      });
      await Task.WhenAll(deleteTasks);

      return true;
    }

    public async Task<List<Application.DTOs.Chapter.ChapterListItemDto>> GetTeamTranslationsBySeriesAsync(int teamId, int seriesId, int userId, CancellationToken ct = default)
    {
      // Verify team membership
      var isMember = await _context.TeamMembers
          .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, ct);

      if (!isMember)
          throw new UnauthorizedAccessException("Bạn không phải là thành viên của nhóm dịch này.");

      // Fetch translations mapped to ChapterListItemDto for UI compatibility
      return await _context.Translations
          .Include(t => t.Chapter)
          .Include(t => t.Permission)
          .Include(t => t.TeamJoins)
          .Include(t => t.TranslationPages)
          .Include(t => t.TranslationText)
          .Where(t => t.Chapter.SeriesId == seriesId && 
                      (t.Permission.TeamId == teamId || t.TeamJoins.Any(tj => tj.TeamId == teamId)))
          .OrderByDescending(t => t.Chapter.ChapterNumber)
          .Select(t => new Application.DTOs.Chapter.ChapterListItemDto
          {
              ChapterId = t.ChapterId, // ID gốc của truyện 
              TranslationId = t.TranslationId, // ID bản dịch
              ChapterNumber = t.Chapter.ChapterNumber,
              Title = t.Chapter.Title, 
              Status = t.QualityStatus.ToString(),
              ModerationStatus = t.ModerationStatus.ToString(),
              PageCount = (t.ContentType == Domain.Entities.ContentType.IMAGE) ? t.TranslationPages.Count : (t.TranslationText != null ? t.TranslationText.WordCount : 0),
              Views = 0, 
              PublishedAt = t.PublishedAt,
              CreatedAt = t.Chapter.CreatedAt
          })
          .ToListAsync(ct);
    }

    public async Task<bool> DeleteTeamTranslationAsync(int translationId, int teamId, int userId, CancellationToken ct = default)
    {
      var isMember = await _context.TeamMembers
          .AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId, ct);

      if (!isMember)
          throw new UnauthorizedAccessException("Bạn không phải là thành viên của nhóm dịch này.");

      var translation = await _context.Translations
          .Include(t => t.Permission)
          .Include(t => t.TeamJoins)
          .FirstOrDefaultAsync(t => t.TranslationId == translationId && 
                                   (t.Permission.TeamId == teamId || t.TeamJoins.Any(tj => tj.TeamId == teamId)), ct);

      if (translation == null)
          throw new KeyNotFoundException("Không tìm thấy bản dịch hoặc bản dịch không thuộc về nhóm của bạn.");

      return await DeleteTranslationAsync(translationId);
    }

    private TranslationResponse MapToDto(Domain.Entities.Translation t)
    {
      // Resolve TeamId/TeamName: primary source is Translation.TeamId, fallback to Permission, fallback to TeamJoins
      var resolvedTeamId = t.TeamId
                           ?? t.Permission?.TeamId
                           ?? t.TeamJoins?.FirstOrDefault(tj => tj.IsPrimary)?.TeamId
                           ?? t.TeamJoins?.FirstOrDefault()?.TeamId;
      var resolvedTeamName = t.Team?.TeamName
                             ?? t.Permission?.Team?.TeamName
                             ?? t.TeamJoins?.FirstOrDefault(tj => tj.IsPrimary)?.Team?.TeamName
                             ?? t.TeamJoins?.FirstOrDefault()?.Team?.TeamName
                             ?? string.Empty;


      return new TranslationResponse
      {
        TranslationId = t.TranslationId,
        ChapterId = t.ChapterId,
        LanguageId = t.LanguageId,
        LanguageName = t.Language?.Name ?? string.Empty,
        TeamId = resolvedTeamId,
        TeamName = resolvedTeamName,
        ContentType = t.ContentType.ToString(),
        QualityStatus = t.QualityStatus.ToString(),
        ModerationStatus = t.ModerationStatus.ToString(),
        PublishedAt = t.PublishedAt,
        IsOfficial = t.IsOfficial,
        IsOutdated = t.IsOutdated,
        IsOrphan = t.IsOrphan,
        Pages = t.TranslationPages?.OrderBy(p => p.PageNumber).Select(p => p.TranslationImageUrl).ToList(),
        TextContent = t.TranslationText?.ContentUrl,
        SeriesId = t.Chapter?.SeriesId,
        SeriesTitle = t.Chapter?.Series?.Title,
        ChapterNumber = (float?)t.Chapter?.ChapterNumber,
        Title = t.Chapter?.Title
      };
    }
  }
}
