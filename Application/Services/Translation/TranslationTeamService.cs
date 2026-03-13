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

        public TranslationTeamService(IMlndexDbContext context, IUserContext userContext)
        {
            _context = context;
            _userContext = userContext;
        }

        public async Task<TranslationTeamDto> CreateTeamAsync(CreateTranslationTeamDto createDto)
        {
            var userId = _userContext.UserId;
            if (userId == null) throw new UnauthorizedAccessException();

            // Check if name already exists
            if (await _context.TranslationTeams.AnyAsync(t => t.TeamName == createDto.TeamName))
            {
                throw new Exception("Team name already exists.");
            }

            var team = new TranslationTeam
            {
                LeaderId = userId.Value,
                TeamName = createDto.TeamName,
                Description = createDto.Description,
                ReputationScore = 100, // Default starting score
                LockStatus = TeamLockStatus.ACTIVE,
                ModerationStatus = ModerationStatus.APPROVED, // Auto approve for now
                IsMonetizationEnabled = false,
                AvatarUrl = createDto.AvatarUrl,
                BannerUrl = createDto.BannerUrl
            };

            _context.TranslationTeams.Add(team);
            await _context.SaveChangesAsync();

            // Auto-add leader as a team member
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

            if (updateDto.Description != null)
            {
                team.Description = updateDto.Description;
            }

            if (updateDto.AvatarUrl != null)
            {
                team.AvatarUrl = updateDto.AvatarUrl;
            }

            if (updateDto.BannerUrl != null)
            {
                team.BannerUrl = updateDto.BannerUrl;
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

            // Remove members first
            var members = await _context.TeamMembers.Where(m => m.TeamId == teamId).ToListAsync();
            _context.TeamMembers.RemoveRange(members);

            _context.TranslationTeams.Remove(team);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<TranslationTeamDto?> GetTeamByIdAsync(int teamId)
        {
            var team = await _context.TranslationTeams
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
            var teams = await _context.TranslationTeams.ToListAsync();
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
                .Where(p => p.TeamId == teamId)
                .ToListAsync();

            return permissions.Select(p => new TeamSeriesDto
            {
                SeriesId = p.SeriesId,
                Title = p.Series?.Title ?? "Unknown",
                CoverImageUrl = p.Series?.CoverImageUrl,
                Status = p.Status == TranslationPermissionStatus.GRANTED ? "active" :
                         p.Status == TranslationPermissionStatus.PENDING ? "pending" : "dropped",
                TotalChapters = _context.Chapters.Count(c => c.SeriesId == p.SeriesId && c.TeamId == teamId),
                LastUpdate = _context.Chapters
                    .Where(c => c.SeriesId == p.SeriesId && c.TeamId == teamId)
                    .Select(c => (DateTime?)c.PublishedAt)
                    .Max(),
                Views = _context.Chapters
                    .Where(c => c.SeriesId == p.SeriesId && c.TeamId == teamId)
                    .Sum(c => c.Views),
                Rating = p.Series?.AverageRating ?? 0
            });
        }

        public async Task<TeamStatsDto> GetTeamStatsAsync(int teamId)
        {
            var chapters = await _context.Chapters
                .Where(c => c.TeamId == teamId)
                .ToListAsync();

            var activeSeriesCount = await _context.TranslationPermissions
                .CountAsync(p => p.TeamId == teamId && p.Status == TranslationPermissionStatus.GRANTED);

            var totalViews = chapters.Sum(c => c.Views);

            return new TeamStatsDto
            {
                TotalViews = totalViews,
                TotalBookmarks = 0, // Placeholder for now - depends on Bookmark entity integration
                ActiveSeriesCount = activeSeriesCount,
                TotalChaptersTranslated = chapters.Count,
                AverageRating = 0 // Placeholder
            };
        }

        public async Task<IEnumerable<TranslationTeamDto>> GetUserTeamsAsync(int userId, int limit = 5)
        {
            var userTeams = await _context.TeamMembers
                .Include(tm => tm.TranslationTeam)
                .ThenInclude(t => t.TeamMembers)
                .Where(tm => tm.UserId == userId && tm.IsActive)
                .OrderByDescending(tm => tm.JoinedAt)
                .Take(limit)
                .Select(tm => new TranslationTeamDto
                {
                    TeamId = tm.TranslationTeam.TeamId,
                    LeaderId = tm.TranslationTeam.LeaderId,
                    TeamName = tm.TranslationTeam.TeamName,
                    Description = tm.TranslationTeam.Description,
                    ReputationScore = tm.TranslationTeam.ReputationScore,
                    LockStatus = tm.TranslationTeam.LockStatus.ToString(),
                    IsMonetizationEnabled = tm.TranslationTeam.IsMonetizationEnabled,
                    ModerationStatus = tm.TranslationTeam.ModerationStatus.ToString(),
                    MemberCount = tm.TranslationTeam.TeamMembers.Count,
                    Role = tm.Role.ToString(),
                    AvatarUrl = tm.TranslationTeam.AvatarUrl,
                    BannerUrl = tm.TranslationTeam.BannerUrl
                })
                .ToListAsync();

            if (!userTeams.Any())
            {
                // Thêm leader condition in case member table missed leader
                userTeams = await _context.TranslationTeams
                    .Include(t => t.TeamMembers)
                    .Where(t => t.LeaderId == userId)
                    .Take(limit)
                    .Select(t => new TranslationTeamDto
                    {
                        TeamId = t.TeamId,
                        LeaderId = t.LeaderId,
                        TeamName = t.TeamName,
                        Description = t.Description,
                        ReputationScore = t.ReputationScore,
                        LockStatus = t.LockStatus.ToString(),
                        IsMonetizationEnabled = t.IsMonetizationEnabled,
                        ModerationStatus = t.ModerationStatus.ToString(),
                        MemberCount = t.TeamMembers.Count,
                        Role = "LEADER",
                        AvatarUrl = t.AvatarUrl,
                        BannerUrl = t.BannerUrl
                    })
                    .ToListAsync();
            }

            return userTeams;
        }

        private TranslationTeamDto MapToDto(TranslationTeam team)
        {
            return new TranslationTeamDto
            {
                TeamId = team.TeamId,
                LeaderId = team.LeaderId,
                TeamName = team.TeamName,
                Description = team.Description,
                ReputationScore = team.ReputationScore,
                LockStatus = team.LockStatus.ToString(),
                IsMonetizationEnabled = team.IsMonetizationEnabled,
                ModerationStatus = team.ModerationStatus.ToString(),
                MemberCount = team.TeamMembers?.Count ?? 0,
                AvatarUrl = team.AvatarUrl,
                BannerUrl = team.BannerUrl,
                // Role field in DTO is primarily for list endpoints where we know the user context.
                // For single team lookup, we might need to set it separately if needed.
            };
        }
    }
}
