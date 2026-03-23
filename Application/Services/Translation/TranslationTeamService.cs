using Application.Interfaces.Notification;
using Application.Interfaces.Common;
using Application.Interfaces.Data;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Translation
{
  public class TranslationTeamService : ITranslationTeamService
  {
    private readonly IMlndexDbContext _context;
    private readonly IUserContext _userContext;
    private readonly INotificationService _notificationService;

    public TranslationTeamService(IMlndexDbContext context, IUserContext userContext, INotificationService notificationService)
    {
      _context = context;
      _userContext = userContext;
      _notificationService = notificationService;
    }

    public async Task<TranslationTeamDto> CreateTeamAsync(CreateTranslationTeamDto createDto)
    {
      var userId = _userContext.UserId;
      if (userId == null) throw new UnauthorizedAccessException();

      if (await _context.TranslationTeams.AnyAsync(t => t.TeamName == createDto.TeamName))
      {
        throw new Exception("Team name already exists.");
      }

      if (await _context.TranslationTeams.AnyAsync(t => t.Slug == createDto.Slug))
      {
        throw new Exception("Slug already exists.");
      }

      var team = new TranslationTeam
      {
        LeaderId = userId.Value,
        TeamName = createDto.TeamName,
        Slug = createDto.Slug,
        Description = createDto.Description,
        LanguageId = createDto.LanguageId,
        RequireApproval = createDto.RequireApproval,
        ReputationScore = 100,
        LockStatus = TeamLockStatus.ACTIVE,
        ModerationStatus = ModerationStatus.APPROVED,
        IsMonetizationEnabled = false,
        AvatarUrl = createDto.AvatarUrl,
        BannerUrl = createDto.BannerUrl,
        Facebook = createDto.Facebook,
        Discord = createDto.Discord,
        Website = createDto.Website,
        Certificates = createDto.Certificates
      };

      _context.TranslationTeams.Add(team);
      await _context.SaveChangesAsync();

      if (createDto.GenreIds != null && createDto.GenreIds.Any())
      {
        foreach (var genreId in createDto.GenreIds)
        {
          _context.TeamGenres.Add(new TeamGenre
          {
            TeamId = team.TeamId,
            GenreId = genreId
          });
        }
        await _context.SaveChangesAsync();
      }

      var member = new TeamMember
      {
        TeamId = team.TeamId,
        UserId = userId.Value,
        Role = TeamMemberRole.LEADER,
        JoinedAt = DateTime.UtcNow,
        IsActive = true
      };

      _context.TeamMembers.Add(member);
      await _context.SaveChangesAsync();

      return await GetTeamByIdAsync(team.TeamId) ?? MapToDto(team);
    }

    public async Task<TranslationTeamDto> UpdateTeamAsync(int teamId, UpdateTranslationTeamDto updateDto)
    {
      var userId = _userContext.UserId;
      var team = await _context.TranslationTeams
          .FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == userId);

      if (team == null) throw new Exception("Team not found or unauthorized.");

      if (!string.IsNullOrEmpty(updateDto.TeamName) && updateDto.TeamName != team.TeamName)
      {
        if (await _context.TranslationTeams.AnyAsync(t => t.TeamName == updateDto.TeamName && t.TeamId != teamId))
        {
          throw new Exception("Team name already exists.");
        }
        team.TeamName = updateDto.TeamName;
      }

      if (!string.IsNullOrEmpty(updateDto.Slug) && updateDto.Slug != team.Slug)
      {
        if (await _context.TranslationTeams.AnyAsync(t => t.Slug == updateDto.Slug && t.TeamId != teamId))
        {
          throw new Exception("Slug already exists.");
        }
        team.Slug = updateDto.Slug;
      }

      if (updateDto.Description != null) team.Description = updateDto.Description;
      if (updateDto.LanguageId.HasValue) team.LanguageId = updateDto.LanguageId.Value;
      if (updateDto.RequireApproval.HasValue) team.RequireApproval = updateDto.RequireApproval.Value;
      if (updateDto.AvatarUrl != null) team.AvatarUrl = updateDto.AvatarUrl;
      if (updateDto.BannerUrl != null) team.BannerUrl = updateDto.BannerUrl;
      if (updateDto.Facebook != null) team.Facebook = updateDto.Facebook;
      if (updateDto.Discord != null) team.Discord = updateDto.Discord;
      if (updateDto.Website != null) team.Website = updateDto.Website;
      if (updateDto.Certificates != null) team.Certificates = updateDto.Certificates;

      if (updateDto.GenreIds != null)
      {
        var currentGenres = await _context.TeamGenres.Where(tg => tg.TeamId == teamId).ToListAsync();
        _context.TeamGenres.RemoveRange(currentGenres);

        foreach (var genreId in updateDto.GenreIds)
        {
          _context.TeamGenres.Add(new TeamGenre
          {
            TeamId = teamId,
            GenreId = genreId
          });
        }
      }

      await _context.SaveChangesAsync();
      return await GetTeamByIdAsync(teamId) ?? MapToDto(team);
    }

    public async Task<bool> DisbandTeamAsync(int teamId)
    {
      var userId = _userContext.UserId;
      var team = await _context.TranslationTeams
          .FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == userId);

      if (team == null) return false;

      var members = await _context.TeamMembers.Where(m => m.TeamId == teamId).ToListAsync();
      _context.TeamMembers.RemoveRange(members);

      _context.TranslationTeams.Remove(team);
      await _context.SaveChangesAsync();
      return true;
    }

    public async Task<TranslationTeamDto?> GetTeamByIdAsync(int teamId)
    {
      var team = await _context.TranslationTeams
          .Include(t => t.TeamGenres)
          .ThenInclude(tg => tg.Genre)
          .Include(t => t.TeamMembers)
          .FirstOrDefaultAsync(t => t.TeamId == teamId);

      if (team == null) return null;
      return MapToDto(team);
    }

    public async Task<IEnumerable<TeamMemberDetailDto>> GetTeamMembersAsync(int teamId)
    {
      return await _context.TeamMembers
          .Include(m => m.User)
          .Where(m => m.TeamId == teamId)
          .Select(m => new TeamMemberDetailDto
          {
            UserId = m.UserId,
            Username = m.User.Username,
            DisplayName = m.User.DisplayName,
            Role = m.Role.ToString(),
            JoinedAt = m.JoinedAt
          })
          .ToListAsync();
    }

    public async Task<IEnumerable<TranslationTeamDto>> GetAllTeamsAsync()
    {
      var teams = await _context.TranslationTeams
          .Include(t => t.TeamGenres)
          .ThenInclude(tg => tg.Genre)
          .Include(t => t.TeamMembers)
          .ToListAsync();
      return teams.Select(MapToDto);
    }

    public async Task<int> InviteMemberAsync(int teamId, InviteTeamMemberDto inviteDto)
    {
      var leaderId = _userContext.UserId;
      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new Exception("Team not found or unauthorized.");

      if (await _context.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == inviteDto.UserId))
      {
        throw new Exception("User is already a team member.");
      }

      if (await _context.TeamInvitations.AnyAsync(i => i.TeamId == teamId && i.InviteeId == inviteDto.UserId && i.Status == TeamInvitationStatus.PENDING))
      {
        throw new Exception("Invitation already pending.");
      }

      var invitation = new TeamInvitation
      {
        TeamId = teamId,
        InviteeId = inviteDto.UserId,
        InviterId = leaderId.Value,
        Role = inviteDto.Role.ToString(),
        Status = TeamInvitationStatus.PENDING,
        CreatedAt = DateTime.UtcNow
      };

      _context.TeamInvitations.Add(invitation);
      await _context.SaveChangesAsync();

      await _notificationService.CreateNotificationAsync(
          inviteDto.UserId,
          team.TeamName,
          $"Mời bạn gia nhập nhóm với vai trò {inviteDto.Role}",
          $"/teams/{teamId}",
          NotificationType.TEAM_INVITATION
      );

      return invitation.InvitationId;
    }

    public async Task<bool> AcceptInvitationAsync(int invitationId)
    {
      var userId = _userContext.UserId;
      var invitation = await _context.TeamInvitations.FirstOrDefaultAsync(i => i.InvitationId == invitationId && i.InviteeId == userId && i.Status == TeamInvitationStatus.PENDING);
      if (invitation == null) return false;

      invitation.Status = TeamInvitationStatus.ACCEPTED;
      invitation.RespondedAt = DateTime.UtcNow;

      var member = new TeamMember
      {
        TeamId = invitation.TeamId,
        UserId = invitation.InviteeId,
        Role = Enum.Parse<TeamMemberRole>(invitation.Role),
        JoinedAt = DateTime.UtcNow,
        IsActive = true
      };

      _context.TeamMembers.Add(member);
      await _context.SaveChangesAsync();

      var invitee = await _context.Users.FindAsync(userId);
      var team = await _context.TranslationTeams.FindAsync(invitation.TeamId);
      if (invitee != null && team != null)
      {
        await _notificationService.CreateNotificationAsync(
            team.LeaderId,
            invitee.DisplayName ?? invitee.Username,
            $"Đã chấp nhận lời mời gia nhập nhóm {team.TeamName}",
            $"/teams/{team.TeamId}/members",
            NotificationType.TEAM_INVITATION_ACCEPTED
        );
      }

      return true;
    }

    public async Task<bool> RejectInvitationAsync(int invitationId)
    {
      var userId = _userContext.UserId;
      var invitation = await _context.TeamInvitations.FirstOrDefaultAsync(i => i.InvitationId == invitationId && i.InviteeId == userId && i.Status == TeamInvitationStatus.PENDING);
      if (invitation == null) return false;

      invitation.Status = TeamInvitationStatus.REJECTED;
      invitation.RespondedAt = DateTime.UtcNow;

      await _context.SaveChangesAsync();

      var invitee = await _context.Users.FindAsync(userId);
      var team = await _context.TranslationTeams.FindAsync(invitation.TeamId);
      if (invitee != null && team != null)
      {
        await _notificationService.CreateNotificationAsync(
            team.LeaderId,
            invitee.DisplayName ?? invitee.Username,
            $"Đã từ chối lời mời gia nhập nhóm {team.TeamName}",
            $"/teams/{team.TeamId}/members",
            NotificationType.TEAM_INVITATION_REJECTED
        );
      }

      return true;
    }

    public async Task<int> RequestToJoinAsync(int teamId, JoinTeamRequestDto joinDto)
    {
      var userId = _userContext.UserId;
      if (userId == null) throw new UnauthorizedAccessException();

      if (await _context.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == userId))
      {
        throw new Exception("You are already a member of this team.");
      }

      if (await _context.TeamJoinRequests.AnyAsync(r => r.TeamId == teamId && r.UserId == userId && r.Status == TeamJoinRequestStatus.PENDING))
      {
        throw new Exception("Join request already pending.");
      }

      var request = new TeamJoinRequest
      {
        TeamId = teamId,
        UserId = userId.Value,
        Message = joinDto.Message,
        Status = TeamJoinRequestStatus.PENDING,
        CreatedAt = DateTime.UtcNow
      };

      _context.TeamJoinRequests.Add(request);
      await _context.SaveChangesAsync();

      var team = await _context.TranslationTeams.FindAsync(teamId);
      var requester = await _context.Users.FindAsync(userId.Value);
      if (team != null && requester != null)
      {
        await _notificationService.CreateNotificationAsync(
            team.LeaderId,
            team.TeamName,
            $"{requester.DisplayName ?? requester.Username} đã gửi yêu cầu tham gia nhóm",
            $"/teams/{teamId}/requests",
            NotificationType.TEAM_JOIN_REQUEST
        );
      }

      return request.RequestId;
    }

    public async Task<bool> ApproveJoinRequestAsync(int requestId)
    {
      var leaderId = _userContext.UserId;
      var request = await _context.TeamJoinRequests.Include(r => r.Team).FirstOrDefaultAsync(r => r.RequestId == requestId && r.Team.LeaderId == leaderId && r.Status == TeamJoinRequestStatus.PENDING);
      if (request == null) return false;

      request.Status = TeamJoinRequestStatus.APPROVED;
      request.RespondedAt = DateTime.UtcNow;

      var member = new TeamMember
      {
        TeamId = request.TeamId,
        UserId = request.UserId,
        Role = TeamMemberRole.TRANSLATOR,
        JoinedAt = DateTime.UtcNow,
        IsActive = true
      };

      _context.TeamMembers.Add(member);
      await _context.SaveChangesAsync();

      if (request.Team != null)
      {
        await _notificationService.CreateNotificationAsync(
            request.UserId,
            request.Team.TeamName,
            "Yêu cầu tham gia nhóm của bạn đã được phê duyệt",
            $"/teams/{request.TeamId}",
            NotificationType.TEAM_JOIN_APPROVED
        );
      }

      return true;
    }

    public async Task<bool> RejectJoinRequestAsync(int requestId)
    {
      var leaderId = _userContext.UserId;
      var request = await _context.TeamJoinRequests.Include(r => r.Team).FirstOrDefaultAsync(r => r.RequestId == requestId && r.Team.LeaderId == leaderId && r.Status == TeamJoinRequestStatus.PENDING);
      if (request == null) return false;

      request.Status = TeamJoinRequestStatus.REJECTED;
      request.RespondedAt = DateTime.UtcNow;

      await _context.SaveChangesAsync();

      if (request.Team != null)
      {
        await _notificationService.CreateNotificationAsync(
            request.UserId,
            request.Team.TeamName,
            "Yêu cầu tham gia nhóm của bạn đã bị từ chối",
            $"/teams/{request.TeamId}",
            NotificationType.TEAM_JOIN_REJECTED
        );
      }

      return true;
    }

    public async Task<IEnumerable<TeamInvitationDto>> GetTeamInvitationsAsync(int teamId)
    {
      var leaderId = _userContext.UserId;
      if (leaderId == null) throw new UnauthorizedAccessException();

      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new Exception("Team not found or unauthorized.");

      return await _context.TeamInvitations
          .Include(i => i.Invitee)
          .Where(i => i.TeamId == teamId && i.Status == TeamInvitationStatus.PENDING)
          .Select(i => new TeamInvitationDto
          {
            InvitationId = i.InvitationId,
            TeamId = i.TeamId,
            UserId = i.InviteeId,
            Username = i.Invitee.Username,
            TargetRole = i.Role,
            Status = i.Status.ToString(),
            InvitedAt = i.CreatedAt,
            ExpiresAt = i.CreatedAt.AddDays(7)
          })
          .ToListAsync();
    }

    public async Task<IEnumerable<TeamJoinRequestDtoResponse>> GetTeamJoinRequestsAsync(int teamId)
    {
      var leaderId = _userContext.UserId;
      if (leaderId == null) throw new UnauthorizedAccessException();

      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new Exception("Team not found or unauthorized.");

      return await _context.TeamJoinRequests
          .Include(r => r.User)
          .Where(r => r.TeamId == teamId && r.Status == TeamJoinRequestStatus.PENDING)
          .Select(r => new TeamJoinRequestDtoResponse
          {
            RequestId = r.RequestId,
            TeamId = r.TeamId,
            UserId = r.UserId,
            Username = r.User.Username,
            Message = r.Message,
            Status = r.Status.ToString(),
            RequestedAt = r.CreatedAt
          })
          .ToListAsync();
    }

    public async Task<bool> RemoveMemberAsync(int teamId, int targetUserId)
    {
      var leaderId = _userContext.UserId;
      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new Exception("Team not found or unauthorized.");

      if (leaderId == targetUserId) throw new Exception("Leader cannot be removed.");

      var member = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId);
      if (member == null) return false;

      _context.TeamMembers.Remove(member);
      await _context.SaveChangesAsync();

      await _notificationService.CreateNotificationAsync(
          targetUserId,
          team.TeamName,
          "Bạn đã bị gỡ khỏi nhóm",
          $"/teams",
          NotificationType.TEAM_MEMBER_REMOVED
      );

      return true;
    }

    public async Task<TeamMemberDto> AssignRoleAsync(int teamId, int targetUserId, AssignTeamMemberRoleDto roleDto)
    {
      var leaderId = _userContext.UserId;
      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new Exception("Team not found or unauthorized.");

      if (leaderId == targetUserId) throw new Exception("Leader role cannot be changed manually.");

      var member = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId);
      if (member == null) throw new Exception("Member not found.");

      member.Role = roleDto.Role;
      await _context.SaveChangesAsync();

      await _notificationService.CreateNotificationAsync(
          targetUserId,
          team.TeamName,
          $"Vai trò của bạn trong nhóm đã được cập nhật thành {roleDto.Role}",
          $"/teams/{teamId}/members",
          NotificationType.TEAM_ROLE_CHANGED
      );

      return new TeamMemberDto
      {
        MembershipId = member.MembershipId,
        TeamId = member.TeamId,
        UserId = member.UserId,
        Role = member.Role.ToString(),
        JoinedAt = member.JoinedAt,
        IsActive = member.IsActive
      };
    }

    public async Task<IEnumerable<TeamSeriesDto>> GetTeamSeriesAsync(int teamId)
    {
      var permissions = await _context.TranslationPermissions
          .Include(p => p.Series)
          .Include(p => p.Language)
          .Where(p => p.TeamId == teamId)
          .ToListAsync();

      return permissions.Select(p => new TeamSeriesDto
      {
        SeriesId = p.SeriesId,
        PermissionId = p.PermissionId,
        LanguageId = p.LanguageId,
        LanguageName = p.Language?.Name ?? "Unknown",
        Title = p.Series?.Title ?? "Unknown",
        CoverImageUrl = p.Series?.CoverImageUrl,
        Status = (p.Status == TranslationPermissionStatus.GRANTED || p.Status == TranslationPermissionStatus.UNOFFICIAL) ? "active" :
                   p.Status == TranslationPermissionStatus.PENDING ? "pending" : "dropped",
        TotalChapters = _context.Translations.Count(t => t.PermissionId == p.PermissionId),
        LastUpdate = _context.Translations
              .Where(t => t.PermissionId == p.PermissionId)
              .Select(t => (DateTime?)t.PublishedAt)
              .Max(),
        Views = 0, // Translation views tracking not yet implemented
        Rating = p.Series?.AverageRating ?? 0
      });
    }

    public async Task<TeamStatsDto> GetTeamStatsAsync(int teamId)
    {
      var translatedChaptersCount = await _context.Translations
          .CountAsync(t => t.Permission.TeamId == teamId);

      var activeSeriesCount = await _context.TranslationPermissions
          .CountAsync(p => p.TeamId == teamId && (p.Status == TranslationPermissionStatus.GRANTED || p.Status == TranslationPermissionStatus.UNOFFICIAL));

      return new TeamStatsDto
      {
        TotalViews = 0, // Translation views tracking not yet implemented
        TotalBookmarks = 0,
        ActiveSeriesCount = activeSeriesCount,
        TotalChaptersTranslated = translatedChaptersCount,
        AverageRating = 0
      };
    }

    public async Task<IEnumerable<TranslationTeamDto>> GetUserTeamsAsync(int userId, int limit = 5)
    {
      var userMembers = await _context.TeamMembers
          .Include(tm => tm.TranslationTeam)
          .ThenInclude(t => t.TeamMembers)
          .Where(tm => tm.UserId == userId && tm.IsActive)
          .OrderByDescending(tm => tm.JoinedAt)
          .Take(limit)
          .ToListAsync();

      var userTeams = userMembers.Select(tm => new TranslationTeamDto
      {
        TeamId = tm.TranslationTeam.TeamId,
        LeaderId = tm.TranslationTeam.LeaderId,
        TeamName = tm.TranslationTeam.TeamName,
        Description = tm.TranslationTeam.Description,
        ReputationScore = tm.TranslationTeam.ReputationScore,
        LockStatus = tm.TranslationTeam.LockStatus.ToString(),
        IsMonetizationEnabled = tm.TranslationTeam.IsMonetizationEnabled,
        ModerationStatus = tm.TranslationTeam.ModerationStatus.ToString(),
        MemberCount = tm.TranslationTeam.TeamMembers?.Count ?? 0,
        Role = tm.Role.ToString(),
        AvatarUrl = tm.TranslationTeam.AvatarUrl,
        BannerUrl = tm.TranslationTeam.BannerUrl
      }).ToList();

      // Also fetch teams where user is leader but might not be in TeamMembers (e.g. from seed data)
      var ledTeams = await _context.TranslationTeams
          .Include(t => t.TeamMembers)
          .Where(t => t.LeaderId == userId)
          .Take(limit)
          .ToListAsync();

      foreach (var t in ledTeams)
      {
        if (!userTeams.Any(ut => ut.TeamId == t.TeamId))
        {
          userTeams.Add(new TranslationTeamDto
          {
            TeamId = t.TeamId,
            LeaderId = t.LeaderId,
            TeamName = t.TeamName,
            Description = t.Description,
            ReputationScore = t.ReputationScore,
            LockStatus = t.LockStatus.ToString(),
            IsMonetizationEnabled = t.IsMonetizationEnabled,
            ModerationStatus = t.ModerationStatus.ToString(),
            MemberCount = t.TeamMembers?.Count ?? 0,
            Role = "LEADER",
            AvatarUrl = t.AvatarUrl,
            BannerUrl = t.BannerUrl
          });
        }
      }

      return userTeams.OrderByDescending(t => t.TeamId).Take(limit);
    }

    private TranslationTeamDto MapToDto(TranslationTeam team)
    {
      return new TranslationTeamDto
      {
        TeamId = team.TeamId,
        LeaderId = team.LeaderId,
        TeamName = team.TeamName,
        Slug = team.Slug ?? string.Empty,
        Description = team.Description,
        LanguageId = team.LanguageId,
        RequireApproval = team.RequireApproval,
        ReputationScore = team.ReputationScore,
        LockStatus = team.LockStatus.ToString(),
        IsMonetizationEnabled = team.IsMonetizationEnabled,
        ModerationStatus = team.ModerationStatus.ToString(),
        MemberCount = team.TeamMembers?.Count ?? 0,
        AvatarUrl = team.AvatarUrl,
        BannerUrl = team.BannerUrl,
        Facebook = team.Facebook,
        Discord = team.Discord,
        Website = team.Website,
        Certificates = team.Certificates,
        Genres = team.TeamGenres?.Select(tg => tg.Genre.Name).ToList()
      };
    }
  }
}
