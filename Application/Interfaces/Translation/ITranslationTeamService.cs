using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
  public interface ITranslationTeamService
  {
    // Translation Team Management
    Task<TranslationTeamDto> CreateTeamAsync(CreateTranslationTeamDto createDto);
    Task<TranslationTeamDto> UpdateTeamAsync(int teamId, UpdateTranslationTeamDto updateDto);
    Task<bool> DisbandTeamAsync(int teamId);
    Task<TranslationTeamDto?> GetTeamByIdAsync(int teamId);
    Task<IEnumerable<TranslationTeamDto>> GetAllTeamsAsync();
    Task<IEnumerable<TeamMemberDetailDto>> GetTeamMembersAsync(int teamId);

    // Team Member Management
    Task<int> InviteMemberAsync(int teamId, InviteTeamMemberDto inviteDto);
    Task<bool> AcceptInvitationAsync(int invitationId);
    Task<bool> RejectInvitationAsync(int invitationId);
    Task<IEnumerable<TeamInvitationDto>> GetTeamInvitationsAsync(int teamId);

    Task<int> RequestToJoinAsync(int teamId, JoinTeamRequestDto joinDto);
    Task<bool> ApproveJoinRequestAsync(int requestId);
    Task<bool> RejectJoinRequestAsync(int requestId);
    Task<IEnumerable<TeamJoinRequestDtoResponse>> GetTeamJoinRequestsAsync(int teamId);

    Task<bool> RemoveMemberAsync(int teamId, int targetUserId);
    Task<bool> LeaveTeamAsync(int teamId);
    Task<TeamMemberDto> AssignRoleAsync(int teamId, int targetUserId, AssignTeamMemberRoleDto roleDto);

    // Team stats and series
    Task<IEnumerable<TeamSeriesDto>> GetTeamSeriesAsync(int teamId);
    Task<TeamStatsDto> GetTeamStatsAsync(int teamId);
    Task<IEnumerable<TranslationTeamDto>> GetUserTeamsAsync(int userId, int limit = 5);

  }
}
