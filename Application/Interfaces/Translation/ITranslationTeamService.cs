using System.Collections.Generic;
using System.Threading.Tasks;
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

    Task<bool> RemoveMemberAsync(int teamId, int targetUserId);
    Task<bool> LeaveTeamAsync(int teamId);
    Task<TeamMemberResponse> AssignRoleAsync(int teamId, int targetUserId, AssignTeamMemberRoleRequest roleDto);

    // Team stats and series
    Task<IEnumerable<TeamSeriesResponse>> GetTeamSeriesAsync(int teamId);
    Task<TeamStatsResponse> GetTeamStatsAsync(int teamId);
    Task<IEnumerable<TranslationTeamResponse>> GetUserTeamsAsync(int userId, int limit = 5);
  }
}
