using Application.DTOs.Common;
using Application.Exceptions;
using Application.Interfaces.Notification;
using Application.Interfaces.Common;
using Application.Interfaces.Data;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;
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

    public async Task<TranslationTeamResponse> CreateTeamAsync(CreateTranslationTeamRequest createDto)
    {
      var userId = _userContext.UserId;
      if (userId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      if (await _context.TranslationTeams.AnyAsync(t => t.TeamName == createDto.TeamName))
      {
        throw new AppException(ErrorCodes.DUPLICATE_TRANSLATION_TEAM);
      }

      if (await _context.TranslationTeams.AnyAsync(t => t.Slug == createDto.Slug))
      {
        throw new AppException(ErrorCodes.DUPLICATE_TEAM_SLUG);
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
        IsMonetizationEnabled = true,
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

    public async Task<TranslationTeamResponse> UpdateTeamAsync(int teamId, UpdateTranslationTeamRequest updateDto)
    {
      var userId = _userContext.UserId;
      var team = await _context.TranslationTeams
          .FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == userId);

      if (team == null) throw new AppException(ErrorCodes.TEAM_NOT_FOUND_OR_UNAUTHORIZED);

      if (!string.IsNullOrEmpty(updateDto.TeamName) && updateDto.TeamName != team.TeamName)
      {
        if (await _context.TranslationTeams.AnyAsync(t => t.TeamName == updateDto.TeamName && t.TeamId != teamId))
        {
          throw new AppException(ErrorCodes.DUPLICATE_TRANSLATION_TEAM);
        }
        team.TeamName = updateDto.TeamName;
      }

      if (!string.IsNullOrEmpty(updateDto.Slug) && updateDto.Slug != team.Slug)
      {
        if (await _context.TranslationTeams.AnyAsync(t => t.Slug == updateDto.Slug && t.TeamId != teamId))
        {
          throw new AppException(ErrorCodes.DUPLICATE_TEAM_SLUG);
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

      // ── Unlock Settings ──
      if (updateDto.UnlockEnabled.HasValue) team.UnlockEnabled = updateDto.UnlockEnabled.Value;
      if (updateDto.DefaultUnlockPriceCoins.HasValue) team.DefaultUnlockPriceCoins = updateDto.DefaultUnlockPriceCoins;
      if (updateDto.FreeAfterEnabled.HasValue) team.FreeAfterEnabled = updateDto.FreeAfterEnabled.Value;
      if (updateDto.DefaultFreeAfterDays.HasValue) team.DefaultFreeAfterDays = updateDto.DefaultFreeAfterDays;

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
      foreach (var m in members)
      {
        m.IsActive = false;
        m.LeftAt = DateTime.UtcNow;
      }

      team.LockStatus = TeamLockStatus.DISBANDED;
      team.UpdatedAt = DateTime.UtcNow;

      await _context.SaveChangesAsync();
      return true;
    }

    public async Task<TranslationTeamResponse?> GetTeamByIdAsync(int teamId)
    {
      var team = await _context.TranslationTeams
          .Include(t => t.TeamGenres)
          .ThenInclude(tg => tg.Genre)
          .Include(t => t.TeamMembers)
          .FirstOrDefaultAsync(t => t.TeamId == teamId);

      if (team == null) return null;
      return MapToDto(team);
    }

    public async Task<IEnumerable<TeamMemberDetailResponse>> GetTeamMembersAsync(int teamId)
    {
      return await _context.TeamMembers
          .Include(m => m.User)
          .Where(m => m.TeamId == teamId)
          .Select(m => new TeamMemberDetailResponse
          {
            UserId = m.UserId,
            Username = m.User!.Username,
            Email = m.User.Email,
            DisplayName = m.User!.DisplayName ?? m.User.Username,
            Role = m.Role.ToString(),
            JoinedAt = m.JoinedAt
          })
          .ToListAsync();
    }

    public async Task<IEnumerable<TranslationTeamResponse>> GetAllTeamsAsync()
    {
      var teams = await _context.TranslationTeams
          .Include(t => t.TeamGenres)
          .ThenInclude(tg => tg.Genre)
          .Include(t => t.TeamMembers)
          .ToListAsync();
      return teams.Select(MapToDto);
    }

    public async Task<int> InviteMemberAsync(int teamId, InviteTeamMemberRequest inviteDto)
    {
      var leaderId = _userContext.UserId;
      if (leaderId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new AppException(ErrorCodes.TEAM_NOT_FOUND_OR_UNAUTHORIZED);

      var existingMember = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == inviteDto.UserId && m.IsActive);
      if (existingMember != null)
      {
        if (inviteDto.Role != TeamMemberRole.LEADER)
          throw new AppException(ErrorCodes.USER_ALREADY_IN_TEAM);
      }

      if (await _context.TeamInvitations.AnyAsync(i => i.TeamId == teamId && i.InviteeId == inviteDto.UserId && i.Status == TeamInvitationStatus.PENDING))
      {
        throw new AppException(ErrorCodes.INVITATION_ALREADY_PENDING);
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
      if (userId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      // Cooldown 24h check
      await CheckLeaveTeamCooldownAsync(userId.Value);

      var invitation = await _context.TeamInvitations.FirstOrDefaultAsync(i => i.InvitationId == invitationId && i.InviteeId == userId && i.Status == TeamInvitationStatus.PENDING);
      if (invitation == null) return false;

      invitation.Status = TeamInvitationStatus.ACCEPTED;
      invitation.RespondedAt = DateTime.UtcNow;

      var teamRole = Enum.Parse<TeamMemberRole>(invitation.Role);
      var team = await _context.TranslationTeams.FindAsync(invitation.TeamId);
      if (team == null) return false;

      var existingMember = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == invitation.TeamId && m.UserId == userId.Value);

      bool isLeadershipTransfer = teamRole == TeamMemberRole.LEADER;
      if (isLeadershipTransfer)
      {
        var oldLeaderMember = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == invitation.TeamId && m.UserId == team.LeaderId);
        if (oldLeaderMember != null)
        {
          oldLeaderMember.Role = TeamMemberRole.TRANSLATOR;
        }
        team.LeaderId = userId.Value;
      }

      if (existingMember != null)
      {
        existingMember.Role = teamRole;
        existingMember.JoinedAt = DateTime.UtcNow;
        existingMember.IsActive = true;
        existingMember.LeftAt = null;
      }
      else
      {
        var member = new TeamMember
        {
          TeamId = invitation.TeamId,
          UserId = invitation.InviteeId,
          Role = teamRole,
          JoinedAt = DateTime.UtcNow,
          IsActive = true
        };
        _context.TeamMembers.Add(member);
      }

      await _context.SaveChangesAsync();

      var invitee = await _context.Users.FindAsync(userId);
      if (invitee != null)
      {
        await _notificationService.CreateNotificationAsync(
            isLeadershipTransfer ? invitation.InviterId : team.LeaderId,
            invitee.DisplayName ?? invitee.Username,
            isLeadershipTransfer ? $"Đã chấp nhận lời mời làm Trưởng Nhóm cho {team.TeamName}" : $"Đã gia nhập nhóm {team.TeamName}",
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

    public async Task<int> RequestToJoinAsync(int teamId, JoinTeamRequest joinDto)
    {
      var userId = _userContext.UserId;
      if (userId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      // Cooldown 24h check
      await CheckLeaveTeamCooldownAsync(userId.Value);

      if (await _context.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == userId && m.IsActive))
      {
        throw new AppException(ErrorCodes.USER_ALREADY_IN_TEAM);
      }

      if (await _context.TeamJoinRequests.AnyAsync(r => r.TeamId == teamId && r.UserId == userId && r.Status == TeamJoinRequestStatus.PENDING))
      {
        throw new AppException(ErrorCodes.JOIN_REQUEST_ALREADY_PENDING);
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

      // Cooldown 24h check for the requesting user
      await CheckLeaveTeamCooldownAsync(request.UserId);

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

    public async Task<IEnumerable<TeamInvitationResponse>> GetTeamInvitationsAsync(int teamId)
    {
      var leaderId = _userContext.UserId;
      if (leaderId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new AppException(ErrorCodes.TEAM_NOT_FOUND_OR_UNAUTHORIZED);

      return await _context.TeamInvitations
          .Include(i => i.Invitee)
          .Where(i => i.TeamId == teamId && i.Status == TeamInvitationStatus.PENDING)
          .Select(i => new TeamInvitationResponse
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

    public async Task<IEnumerable<TeamJoinRequestResponse>> GetTeamJoinRequestsAsync(int teamId)
    {
      var leaderId = _userContext.UserId;
      if (leaderId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new AppException(ErrorCodes.TEAM_NOT_FOUND_OR_UNAUTHORIZED);

      return await _context.TeamJoinRequests
          .Include(r => r.User)
          .Where(r => r.TeamId == teamId && r.Status == TeamJoinRequestStatus.PENDING)
          .Select(r => new TeamJoinRequestResponse
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
      if (leaderId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new AppException(ErrorCodes.TEAM_NOT_FOUND_OR_UNAUTHORIZED);

      if (leaderId.Value == targetUserId) throw new AppException(ErrorCodes.CANNOT_REMOVE_LEADER);

      var member = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId && m.IsActive);
      if (member == null) return false;

      member.IsActive = false;
      member.LeftAt = DateTime.UtcNow;
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

    public async Task<bool> LeaveTeamAsync(int teamId)
    {
      var userId = _userContext.UserId;
      if (userId == null) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var team = await _context.TranslationTeams.FindAsync(teamId);
      if (team == null) throw new AppException(ErrorCodes.TEAM_NOT_FOUND);

      // Leader cannot leave — must disband or transfer leadership
      if (team.LeaderId == userId.Value)
        throw new AppException(ErrorCodes.LEADER_CANNOT_LEAVE_TEAM);

      var member = await _context.TeamMembers
          .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId.Value && m.IsActive);
      if (member == null) return false;

      // Soft-delete: mark inactive + record leave time for 24h cooldown
      member.IsActive = false;
      member.LeftAt = DateTime.UtcNow;
      await _context.SaveChangesAsync();

      // Notify all remaining active members
      var leavingUser = await _context.Users.FindAsync(userId.Value);
      var displayName = leavingUser?.DisplayName ?? leavingUser?.Username ?? "Thành viên";
      var username = leavingUser?.Username ?? "";

      var remainingMembers = await _context.TeamMembers
          .Where(m => m.TeamId == teamId && m.IsActive && m.UserId != userId.Value)
          .Select(m => m.UserId)
          .ToListAsync();

      // Also notify Leader if not in TeamMembers table (seed data edge case)
      if (!remainingMembers.Contains(team.LeaderId))
        remainingMembers.Add(team.LeaderId);

      foreach (var memberId in remainingMembers)
      {
        await _notificationService.CreateNotificationAsync(
            memberId,
            displayName,
            $"Đã rời khỏi nhóm {team.TeamName}",
            $"/profile/{username}",
            NotificationType.TEAM_MEMBER_LEFT
        );
      }

      return true;
    }

    public async Task<TeamMemberResponse> AssignRoleAsync(int teamId, int targetUserId, AssignTeamMemberRoleRequest roleDto)
    {
      var leaderId = _userContext.UserId;
      var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
      if (team == null) throw new AppException(ErrorCodes.TEAM_NOT_FOUND_OR_UNAUTHORIZED);

      if (leaderId == targetUserId) throw new AppException(ErrorCodes.CANNOT_CHANGE_LEADER_ROLE);
      if (roleDto.Role == TeamMemberRole.LEADER) throw new AppException(ErrorCodes.LEADERSHIP_TRANSFER_REQUIRES_INVITATION);

      var member = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId);
      if (member == null) throw new AppException(ErrorCodes.USER_NOT_FOUND);

      member.Role = roleDto.Role;
      await _context.SaveChangesAsync();

      await _notificationService.CreateNotificationAsync(
          targetUserId,
          team.TeamName,
          $"Vai trò của bạn trong nhóm đã được cập nhật thành {roleDto.Role}",
          $"/teams/{teamId}/members",
          NotificationType.TEAM_ROLE_CHANGED
      );

      return new TeamMemberResponse
      {
        MembershipId = member.MembershipId,
        TeamId = member.TeamId,
        UserId = member.UserId,
        Role = member.Role.ToString(),
        JoinedAt = member.JoinedAt,
        IsActive = member.IsActive
      };
    }

    public async Task<IEnumerable<TeamSeriesResponse>> GetTeamSeriesAsync(int teamId)
    {
      var permissions = await _context.TranslationPermissions
          .Include(p => p.Series)
          .Include(p => p.Language)
          .Where(p => p.TeamId == teamId)
          .ToListAsync();

      return permissions.Select(p => new TeamSeriesResponse
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

    public async Task<TeamStatsResponse> GetTeamStatsAsync(int teamId)
    {
      var translatedChaptersCount = await _context.Translations
          .CountAsync(t => t.TeamId == teamId);

      var activeSeriesCount = await _context.TranslationPermissions
          .CountAsync(p => p.TeamId == teamId && (p.Status == TranslationPermissionStatus.GRANTED || p.Status == TranslationPermissionStatus.UNOFFICIAL));

      return new TeamStatsResponse
      {
        TotalViews = 0, // Translation views tracking not yet implemented
        TotalBookmarks = 0,
        ActiveSeriesCount = activeSeriesCount,
        TotalChaptersTranslated = translatedChaptersCount,
        AverageRating = 0
      };
    }

    public async Task<IEnumerable<TranslationTeamResponse>> GetUserTeamsAsync(int userId, int limit = 5)
    {
      var userMembers = await _context.TeamMembers
          .Include(tm => tm.TranslationTeam)
          .ThenInclude(t => t.TeamMembers)
          .Where(tm => tm.UserId == userId && tm.IsActive)
          .OrderByDescending(tm => tm.JoinedAt)
          .Take(limit)
          .ToListAsync();

      var userTeams = userMembers.Select(tm => new TranslationTeamResponse
      {
        TeamId = tm.TranslationTeam.TeamId,
        LeaderId = tm.TranslationTeam.LeaderId,
        TeamName = tm.TranslationTeam.TeamName,
        Description = tm.TranslationTeam.Description,
        ReputationScore = tm.TranslationTeam.ReputationScore,
        LockStatus = tm.TranslationTeam.LockStatus.ToString(),
        IsMonetizationEnabled = tm.TranslationTeam.IsMonetizationEnabled,
        ModerationStatus = tm.TranslationTeam.ModerationStatus.ToString(),
        MemberCount = tm.TranslationTeam.TeamMembers?.Count(m => m.IsActive) ?? 0,
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
          userTeams.Add(new TranslationTeamResponse
          {
            TeamId = t.TeamId,
            LeaderId = t.LeaderId,
            TeamName = t.TeamName,
            Description = t.Description,
            ReputationScore = t.ReputationScore,
            LockStatus = t.LockStatus.ToString(),
            IsMonetizationEnabled = t.IsMonetizationEnabled,
            ModerationStatus = t.ModerationStatus.ToString(),
            MemberCount = t.TeamMembers?.Count(m => m.IsActive) ?? 0,
            Role = "LEADER",
            AvatarUrl = t.AvatarUrl,
            BannerUrl = t.BannerUrl
          });
        }
      }

      return userTeams.OrderByDescending(t => t.TeamId).Take(limit);
    }

    private async Task CheckLeaveTeamCooldownAsync(int userId)
    {
      var cutoff = DateTime.UtcNow.AddHours(-24);
      var recentLeave = await _context.TeamMembers
          .Where(m => m.UserId == userId && !m.IsActive && m.LeftAt != null && m.LeftAt > cutoff)
          .OrderByDescending(m => m.LeftAt)
          .FirstOrDefaultAsync();

      if (recentLeave != null && recentLeave.LeftAt.HasValue)
      {
        var remaining = recentLeave.LeftAt.Value.AddHours(24) - DateTime.UtcNow;
        var hours = (int)remaining.TotalHours;
        var minutes = remaining.Minutes;
        throw new AppException(ErrorCodes.TEAM_JOIN_COOLDOWN);
      }
    }

    private TranslationTeamResponse MapToDto(TranslationTeam team)
    {
      return new TranslationTeamResponse
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
        MemberCount = team.TeamMembers?.Count(m => m.IsActive) ?? 0,
        AvatarUrl = team.AvatarUrl,
        BannerUrl = team.BannerUrl,
        Facebook = team.Facebook,
        Discord = team.Discord,
        Website = team.Website,
        Certificates = team.Certificates,
        Genres = team.TeamGenres?.Select(tg => tg.Genre?.Name ?? "Unknown").ToList(),

        // ── Unlock Settings ──
        UnlockEnabled = team.UnlockEnabled,
        DefaultUnlockPriceCoins = team.DefaultUnlockPriceCoins,
        FreeAfterEnabled = team.FreeAfterEnabled,
        DefaultFreeAfterDays = team.DefaultFreeAfterDays
      };
    }
  }
}


