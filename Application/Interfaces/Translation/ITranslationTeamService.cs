using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;

namespace Application.Interfaces.Translation
{
  public interface ITranslationTeamService
  {
    // Translation Team Management
    Task<TranslationTeamResponse> CreateTeamAsync(CreateTranslationTeamRequest createDto);
    Task<TranslationTeamResponse> UpdateTeamAsync(int teamId, UpdateTranslationTeamRequest updateDto);
    Task<bool> DisbandTeamAsync(int teamId);
    Task<TranslationTeamResponse?> GetTeamByIdAsync(int teamId);
    Task<IEnumerable<TranslationTeamResponse>> GetAllTeamsAsync();
    Task<IEnumerable<TeamMemberDetailResponse>> GetTeamMembersAsync(int teamId);

    // Team Member Management
    Task<int> InviteMemberAsync(int teamId, InviteTeamMemberRequest inviteDto);
    Task<bool> AcceptInvitationAsync(int invitationId);
    Task<bool> RejectInvitationAsync(int invitationId);
    Task<IEnumerable<TeamInvitationResponse>> GetTeamInvitationsAsync(int teamId);

    Task<int> RequestToJoinAsync(int teamId, JoinTeamRequest joinDto);
    Task<bool> ApproveJoinRequestAsync(int requestId);
    Task<bool> RejectJoinRequestAsync(int requestId);
    Task<IEnumerable<TeamJoinRequestResponse>> GetTeamJoinRequestsAsync(int teamId);

    Task<bool> RemoveMemberAsync(int teamId, int targetUserId);
    Task<bool> LeaveTeamAsync(int teamId);
    Task<TeamMemberResponse> AssignRoleAsync(int teamId, int targetUserId, AssignTeamMemberRoleRequest roleDto);

    // Team stats and series
    Task<IEnumerable<TeamSeriesDto>> GetTeamSeriesAsync(int teamId);
    Task<TeamStatsDto> GetTeamStatsAsync(int teamId);
    Task<IEnumerable<TranslationTeamResponse>> GetUserTeamsAsync(int userId, int limit = 5);
  }
}
