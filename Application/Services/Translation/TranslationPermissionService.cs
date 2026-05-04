using Application.DTOs.Common;
using Application.Exceptions;
using Application.Interfaces.Common;
using Application.Interfaces.Data;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;
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

    public async Task<TranslationPermissionResponse> RequestPermissionAsync(RequestPermissionRequest dto)
    {
      var requesterId = _userContext.UserId;
      if (requesterId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var isMember = await _context.TeamMembers
          .AnyAsync(m => m.TeamId == dto.TeamId && m.UserId == requesterId && m.IsActive);

      var isLeader = await _context.TranslationTeams
          .AnyAsync(t => t.TeamId == dto.TeamId && t.LeaderId == requesterId);

      if (!isMember && !isLeader)
        throw new AppException(ErrorCodes.NOT_TEAM_MEMBER);

      var series = await _context.Series
          .FirstOrDefaultAsync(s => s.SeriesId == dto.SeriesId);

      if (series == null)
        throw new AppException(ErrorCodes.SERIES_NOT_FOUND);

      var creatorProfile = await _context.CreatorProfiles
          .FirstOrDefaultAsync(c => c.CreatorId == series.CreatorId);

      if (creatorProfile == null)
        throw new AppException(ErrorCodes.CREATOR_NOT_FOUND);

      var language = await _context.Languages.FindAsync(dto.LanguageId);
      if (language == null)
        throw new AppException(ErrorCodes.LANGUAGE_NOT_FOUND);

      if (!dto.IsUnofficial)
      {
        var alreadyGranted = await _context.TranslationPermissions
            .AnyAsync(p => p.SeriesId == dto.SeriesId && p.LanguageId == dto.LanguageId && p.Status == TranslationPermissionStatus.GRANTED);
        if (alreadyGranted)
        {
          throw new AppException(ErrorCodes.LANGUAGE_ALREADY_TRANSLATED);
        }
      }

      var existingPermission = await _context.TranslationPermissions
          .FirstOrDefaultAsync(p => p.SeriesId == dto.SeriesId && p.TeamId == dto.TeamId && p.LanguageId == dto.LanguageId);

      TranslationPermission savedPermission;

      if (existingPermission != null)
      {
        if (existingPermission.Status == TranslationPermissionStatus.PENDING)
          throw new AppException(ErrorCodes.PERMISSION_REQUEST_PENDING);
        if (existingPermission.Status == TranslationPermissionStatus.GRANTED)
          throw new AppException(ErrorCodes.PERMISSION_ALREADY_GRANTED);

        // Allow re-requests from DENIED, UNOFFICIAL, or REVOKED
        existingPermission.Status = dto.IsUnofficial ? TranslationPermissionStatus.UNOFFICIAL : TranslationPermissionStatus.PENDING;
        existingPermission.Note = dto.Note;
        existingPermission.CreatedAt = DateTime.UtcNow;
        existingPermission.GrantedAt = dto.IsUnofficial ? DateTime.UtcNow : null;
        existingPermission.RevokedAt = null;

        _context.TranslationPermissions.Update(existingPermission);
        savedPermission = existingPermission;
        await _context.SaveChangesAsync();
      }
      else
      {
        var permission = new TranslationPermission
        {
          SeriesId = dto.SeriesId,
          TeamId = dto.TeamId,
          LanguageId = dto.LanguageId,
          Origin = PermissionOrigin.REQUESTED_BY_TEAM,
          GrantedBy = creatorProfile.UserId,
          Status = dto.IsUnofficial ? TranslationPermissionStatus.UNOFFICIAL : TranslationPermissionStatus.PENDING,
          Note = dto.Note,
          GrantedAt = dto.IsUnofficial ? DateTime.UtcNow : null
        };

        try 
        {
            _context.TranslationPermissions.Add(permission);
            await _context.SaveChangesAsync();
            savedPermission = permission;
        }
        catch (DbUpdateException)
        {
            ((DbContext)_context).ChangeTracker.Clear();
            existingPermission = await _context.TranslationPermissions
                .FirstOrDefaultAsync(p => p.SeriesId == dto.SeriesId && p.TeamId == dto.TeamId && p.LanguageId == dto.LanguageId);
            
            if (existingPermission != null)
            {
                savedPermission = existingPermission;
                // If it exists, we just return it instead of throwing to mimic idempotency on concurrent requests
            }
            else
            {
                throw new AppException(ErrorCodes.PERMISSION_DENIED, "Could not create or retrieve translation permission due to a concurrency issue.");
            }
        }
      }

      var teamName = await _context.TranslationTeams.Where(t => t.TeamId == dto.TeamId).Select(t => t.TeamName).FirstOrDefaultAsync();
      var creatorUserId = await _context.CreatorProfiles
          .Where(c => c.CreatorId == series.CreatorId)
          .Select(c => c.UserId)
          .FirstOrDefaultAsync();

      var notifTitle = dto.IsUnofficial ? "Bản dịch Unofficial mới" : "Yêu cầu dịch truyện mới";
      var notifMessage = dto.IsUnofficial
          ? $"Nhóm dịch {teamName ?? "đối tác"} vừa bắt đầu dịch Unofficial bộ truyện {series.Title} sang ngôn ngữ {language.Name} của bạn."
          : $"Nhóm dịch {teamName ?? "đối tác"} vừa gửi yêu cầu muốn dịch bộ truyện {series.Title} sang ngôn ngữ {language.Name} của bạn.";

      await _notificationService.CreateNotificationAsync(
          creatorUserId,
          notifTitle,
          notifMessage,
          "/creator/translation-requests",
          dto.IsUnofficial ? NotificationType.SYSTEM : NotificationType.TRANSLATION_REQUEST
      );

      return await MapToDtoAsync(savedPermission);
    }

    public async Task<TranslationPermissionResponse> ReviewPermissionAsync(int permissionId, ReviewPermissionRequest dto)
    {
      var creatorId = _userContext.UserId;
      if (creatorId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var permission = await _context.TranslationPermissions
          .Include(p => p.Series)
          .FirstOrDefaultAsync(p => p.PermissionId == permissionId);

      if (permission == null)
        throw new AppException(ErrorCodes.PERMISSION_REQUEST_NOT_FOUND);

      var seriesCreatorUserId = await _context.CreatorProfiles
          .Where(c => c.CreatorId == permission.Series.CreatorId)
          .Select(c => c.UserId)
          .FirstOrDefaultAsync();

      if (seriesCreatorUserId != creatorId)
        throw new AppException(ErrorCodes.CREATOR_ONLY_REVIEW);

      if (dto.IsApproved)
      {
        var alreadyGranted = await _context.TranslationPermissions
            .AnyAsync(p => p.SeriesId == permission.SeriesId && p.LanguageId == permission.LanguageId && p.Status == TranslationPermissionStatus.GRANTED && p.PermissionId != permissionId);
        if (alreadyGranted)
        {
          throw new AppException(ErrorCodes.LANGUAGE_ALREADY_TRANSLATED);
        }

        permission.Status = TranslationPermissionStatus.GRANTED;
        permission.GrantedAt = DateTime.UtcNow;

        // Auto-deny other pending requests for the same series and language
        var pendingRequests = await _context.TranslationPermissions
            .Where(p => p.SeriesId == permission.SeriesId && p.LanguageId == permission.LanguageId && p.Status == TranslationPermissionStatus.PENDING && p.PermissionId != permissionId)
            .ToListAsync();

        foreach (var pr in pendingRequests)
        {
          pr.Status = TranslationPermissionStatus.DENIED;
          pr.RevokedAt = DateTime.UtcNow;
          pr.Note = "Đã có nhóm dịch chính thức phụ trách ngôn ngữ này.";

          // Notify the denied groups
          var prTeamMembers = await _context.TeamMembers
              .Where(m => m.TeamId == pr.TeamId && m.IsActive)
              .Select(m => m.UserId)
              .ToListAsync();

          var prLangName = await _context.Languages
              .Where(l => l.LanguageId == pr.LanguageId)
              .Select(l => l.Name)
              .FirstOrDefaultAsync();

          foreach (var memberId in prTeamMembers)
          {
            await _notificationService.CreateNotificationAsync(
                memberId,
                "Yêu cầu dịch truyện bị từ chối",
                $"Yêu cầu dịch bộ truyện {permission.Series.Title} sang ngôn ngữ {prLangName} của nhóm bạn đã bị từ chối do tác giả đã cấp quyền cho một nhóm khác.",
                $"/translation/sent-requests/{pr.TeamId}",
                NotificationType.TRANSLATION_REVOKED
            );
          }
        }
      }
      else
      {
        permission.Status = TranslationPermissionStatus.DENIED;
        permission.RevokedAt = DateTime.UtcNow;
      }

      permission.GrantedBy = seriesCreatorUserId;

      // Sync the official status to all existing translations associated with this permission
      var existingTranslations = await _context.Translations
          .Where(t => t.PermissionId == permissionId)
          .ToListAsync();

      foreach (var t in existingTranslations)
      {
        t.IsOfficial = dto.IsApproved;
      }

      await _context.SaveChangesAsync();

      var teamMembers = await _context.TeamMembers
          .Where(m => m.TeamId == permission.TeamId && m.IsActive)
          .Select(m => m.UserId)
          .ToListAsync();

      var languageName = await _context.Languages
          .Where(l => l.LanguageId == permission.LanguageId)
          .Select(l => l.Name)
          .FirstOrDefaultAsync();

      var resultTitle = dto.IsApproved ? "Yêu cầu dịch truyện được chấp thuận" : "Yêu cầu dịch truyện bị từ chối";
      var resultMessage = dto.IsApproved
          ? $"Tác giả bộ truyện {permission.Series.Title} đã chấp thuận yêu cầu dịch sang ngôn ngữ {languageName} của nhóm bạn. Bạn đã có thể bắt đầu đăng chương mới."
          : $"Tác giả bộ truyện {permission.Series.Title} đã từ chối yêu cầu dịch sang ngôn ngữ {languageName} của nhóm bạn.";
      var link = $"/translation/sent-requests/{permission.TeamId}";

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

    public async Task<TranslationPermissionResponse> RevokePermissionAsync(int permissionId)
    {
      var creatorId = _userContext.UserId;
      if (creatorId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var permission = await _context.TranslationPermissions
          .Include(p => p.Series)
          .FirstOrDefaultAsync(p => p.PermissionId == permissionId);

      if (permission == null)
        throw new AppException(ErrorCodes.PERMISSION_REQUEST_NOT_FOUND);

      var seriesCreatorUserId = await _context.CreatorProfiles
          .Where(c => c.CreatorId == permission.Series.CreatorId)
          .Select(c => c.UserId)
          .FirstOrDefaultAsync();

      if (seriesCreatorUserId != creatorId)
        throw new AppException(ErrorCodes.CREATOR_ONLY_REVIEW);

      if (permission.Status != TranslationPermissionStatus.GRANTED)
        throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

      permission.Status = TranslationPermissionStatus.REVOKED;
      permission.RevokedAt = DateTime.UtcNow;

      // Sync the official status to all existing translations associated with this permission
      var existingTranslations = await _context.Translations
          .Where(t => t.PermissionId == permissionId)
          .ToListAsync();

      foreach (var t in existingTranslations)
      {
        t.IsOfficial = false;
      }

      await _context.SaveChangesAsync();

      var teamMembers = await _context.TeamMembers
          .Where(m => m.TeamId == permission.TeamId && m.IsActive)
          .Select(m => m.UserId)
          .ToListAsync();

      var languageName = await _context.Languages
          .Where(l => l.LanguageId == permission.LanguageId)
          .Select(l => l.Name)
          .FirstOrDefaultAsync();

      var resultTitle = "Quyền dịch truyện bị thu hồi";
      var resultMessage = $"Tác giả bộ truyện {permission.Series.Title} đã thu hồi quyền dịch sang ngôn ngữ {languageName} của nhóm không còn là nhóm dịch chính thức. Các chương đã đăng sẽ chuyển về dạng Unofficial.";
      var link = $"/translation/sent-requests/{permission.TeamId}";

      foreach (var memberId in teamMembers)
      {
        await _notificationService.CreateNotificationAsync(
            memberId,
            resultTitle,
            resultMessage,
            link,
            NotificationType.TRANSLATION_REVOKED
        );
      }

      return await MapToDtoAsync(permission);
    }

    public async Task<IEnumerable<TranslationPermissionResponse>> GetTeamPermissionsAsync(int teamId)
    {
      var userId = _userContext.UserId;
      if (userId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var isMember = await _context.TeamMembers
          .AnyAsync(m => m.TeamId == teamId && m.UserId == userId && m.IsActive);

      var isLeader = await _context.TranslationTeams
          .AnyAsync(t => t.TeamId == teamId && t.LeaderId == userId);

      if (!isMember && !isLeader)
        throw new AppException(ErrorCodes.TEAM_MEMBER_ONLY_VIEW);

      var permissions = await _context.TranslationPermissions
          .Where(p => p.TeamId == teamId)
          .OrderByDescending(p => p.PermissionId)
          .ToListAsync();

      var dtos = new List<TranslationPermissionResponse>();
      foreach (var p in permissions)
      {
        dtos.Add(await MapToDtoAsync(p));
      }
      return dtos;
    }

    public async Task<IEnumerable<TranslationPermissionResponse>> GetCreatorPermissionsAsync(int userId)
    {
      var creatorId = await _context.CreatorProfiles
          .Where(c => c.UserId == userId)
          .Select(c => c.CreatorId)
          .FirstOrDefaultAsync();

      if (creatorId == 0) return new List<TranslationPermissionResponse>();

      var seriesIds = await _context.Series
          .Where(s => s.CreatorId == creatorId)
          .Select(s => s.SeriesId)
          .ToListAsync();

      var permissions = await _context.TranslationPermissions
          .Where(p => seriesIds.Contains(p.SeriesId))
          .OrderByDescending(p => p.PermissionId)
          .ToListAsync();

      var dtos = new List<TranslationPermissionResponse>();
      foreach (var p in permissions)
      {
        dtos.Add(await MapToDtoAsync(p));
      }
      return dtos;
    }

    private async Task<TranslationPermissionResponse> MapToDtoAsync(TranslationPermission p)
    {
      var seriesTitle = await _context.Series.Where(s => s.SeriesId == p.SeriesId).Select(s => s.Title).FirstOrDefaultAsync();
      var team = await _context.TranslationTeams.Where(t => t.TeamId == p.TeamId).FirstOrDefaultAsync();
      var language = await _context.Languages.Where(l => l.LanguageId == p.LanguageId).FirstOrDefaultAsync();

      return new TranslationPermissionResponse
      {
        PermissionId = p.PermissionId,
        SeriesId = p.SeriesId,
        SeriesTitle = seriesTitle,
        TeamId = p.TeamId,
        TeamName = team?.TeamName,
        LanguageId = p.LanguageId,
        LanguageName = language?.Name,
        Origin = p.Origin.ToString(),
        GrantedBy = p.GrantedBy,
        Status = p.Status.ToString(),
        RequestedAt = p.CreatedAt,
        GrantedAt = p.GrantedAt,
        RevokedAt = p.RevokedAt,
        Note = p.Note,
        Facebook = team?.Facebook,
        Discord = team?.Discord,
        Website = team?.Website,
        Certificates = team?.Certificates
      };
    }
    public async Task<int> AutoDenyExpiredRequestsAsync(int expireHours = 72)
    {
      var cutoffTime = DateTime.UtcNow.AddHours(-expireHours);

      var expiredPermissions = await _context.TranslationPermissions
          .Include(p => p.Series)
          .Where(p => p.Status == TranslationPermissionStatus.PENDING && p.CreatedAt < cutoffTime)
          .ToListAsync();

      if (!expiredPermissions.Any()) return 0;

      foreach (var permission in expiredPermissions)
      {
        permission.Status = TranslationPermissionStatus.DENIED;
        permission.RevokedAt = DateTime.UtcNow;
        permission.Note = "Tự động từ chối do quá 72h không có xác nhận từ tác giả.";

        // Notify team members
        var teamMembers = await _context.TeamMembers
            .Where(m => m.TeamId == permission.TeamId && m.IsActive)
            .Select(m => m.UserId)
            .ToListAsync();

        var languageName = await _context.Languages
            .Where(l => l.LanguageId == permission.LanguageId)
            .Select(l => l.Name)
            .FirstOrDefaultAsync();

        var resultTitle = "Yêu cầu dịch truyện bị từ chối tự động";
        var resultMessage = $"Yêu cầu dịch bộ truyện {permission.Series.Title} sang ngôn ngữ {languageName} của nhóm bạn đã bị hệ thống từ chối tự động do tác giả không phản hồi sau 72h.";
        var link = $"/translation/sent-requests/{permission.TeamId}";

        foreach (var memberId in teamMembers)
        {
          await _notificationService.CreateNotificationAsync(
              memberId,
              resultTitle,
              resultMessage,
              link,
              NotificationType.TRANSLATION_REVOKED
          );
        }

        // Notify creator
        var creatorUserId = await _context.CreatorProfiles
            .Where(c => c.CreatorId == permission.Series.CreatorId)
            .Select(c => c.UserId)
            .FirstOrDefaultAsync();

        if (creatorUserId != 0)
        {
          var teamName = await _context.TranslationTeams.Where(t => t.TeamId == permission.TeamId).Select(t => t.TeamName).FirstOrDefaultAsync();
          await _notificationService.CreateNotificationAsync(
              creatorUserId,
              "Yêu cầu dịch bị hủy tự động",
              $"Yêu cầu dịch bộ truyện {permission.Series.Title} sang ngôn ngữ {languageName} của nhóm {teamName} đã bị hệ thống hủy do bạn không phản hồi sau 72h.",
              "/creator/translation-requests",
              NotificationType.SYSTEM
          );
        }
      }

      await _context.SaveChangesAsync();
      return expiredPermissions.Count;
    }
  }
}
