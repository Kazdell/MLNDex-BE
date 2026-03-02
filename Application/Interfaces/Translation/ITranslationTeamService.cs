using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
    public interface ITranslationTeamService
    {
        // Translation Team Management
        Task<TranslationTeamDto> CreateTeamAsync(int leaderId, CreateTranslationTeamDto createDto);
        Task<bool> DisbandTeamAsync(int teamId, int leaderId);
        Task<TranslationTeamDto?> GetTeamByIdAsync(int teamId);
        Task<IEnumerable<TranslationTeamDto>> GetAllTeamsAsync();

        // Team Member Management
        Task<TeamMemberDto> InviteMemberAsync(int teamId, int leaderId, InviteTeamMemberDto inviteDto);
        Task<bool> RemoveMemberAsync(int teamId, int leaderId, int targetUserId);
        Task<TeamMemberDto> AssignRoleAsync(int teamId, int leaderId, int targetUserId, AssignTeamMemberRoleDto roleDto);
    }
}
