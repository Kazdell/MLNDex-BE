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

        public TranslationTeamService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<TranslationTeamDto> CreateTeamAsync(int leaderId, CreateTranslationTeamDto createDto)
        {
            // Check if name already exists
            if (await _context.TranslationTeams.AnyAsync(t => t.TeamName == createDto.TeamName))
            {
                throw new Exception("Team name already exists.");
            }

            var team = new TranslationTeam
            {
                LeaderId = leaderId,
                TeamName = createDto.TeamName,
                Description = createDto.Description,
                ReputationScore = 100, // Default starting score
                LockStatus = TeamLockStatus.ACTIVE,
                ModerationStatus = ModerationStatus.APPROVED, // Auto approve or pending based on business logic
                IsMonetizationEnabled = false
            };

            _context.TranslationTeams.Add(team);
            await _context.SaveChangesAsync();

            // Auto-add leader as a team member
            var member = new TeamMember
            {
                TeamId = team.TeamId,
                UserId = leaderId,
                Role = TeamMemberRole.LEADER,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };
            
            _context.TeamMembers.Add(member);
            await _context.SaveChangesAsync();

            return MapToDto(team);
        }

        public async Task<bool> DisbandTeamAsync(int teamId, int leaderId)
        {
            var team = await _context.TranslationTeams
                .FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);

            if (team == null) return false;

            // Remove members first
            var members = await _context.TeamMembers.Where(m => m.TeamId == teamId).ToListAsync();
            _context.TeamMembers.RemoveRange(members);

            // Remove team (assuming chapters and permissions are handled/cascading or we should reassign)
            // For now, simplicity:
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

        public async Task<IEnumerable<TranslationTeamDto>> GetAllTeamsAsync()
        {
            var teams = await _context.TranslationTeams.ToListAsync();
            return teams.Select(MapToDto);
        }

        public async Task<TeamMemberDto> InviteMemberAsync(int teamId, int leaderId, InviteTeamMemberDto inviteDto)
        {
            // Check if user is leader
            var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
            if (team == null) throw new Exception("Team not found or unauthorized.");

            // Check if user is already a member
            if (await _context.TeamMembers.AnyAsync(m => m.TeamId == teamId && m.UserId == inviteDto.UserId))
            {
                throw new Exception("User is already a team member.");
            }

            var member = new TeamMember
            {
                TeamId = teamId,
                UserId = inviteDto.UserId,
                Role = inviteDto.Role,
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.TeamMembers.Add(member);
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

        public async Task<bool> RemoveMemberAsync(int teamId, int leaderId, int targetUserId)
        {
            var team = await _context.TranslationTeams.FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == leaderId);
            if (team == null) throw new Exception("Team not found or unauthorized.");

            if (leaderId == targetUserId) throw new Exception("Leader cannot be removed.");

            var member = await _context.TeamMembers.FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == targetUserId);
            if (member == null) return false;

            _context.TeamMembers.Remove(member);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<TeamMemberDto> AssignRoleAsync(int teamId, int leaderId, int targetUserId, AssignTeamMemberRoleDto roleDto)
        {
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
                ModerationStatus = team.ModerationStatus.ToString()
            };
        }
    }
}
