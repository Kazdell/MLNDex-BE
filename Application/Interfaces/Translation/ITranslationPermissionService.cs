using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;

namespace Application.Interfaces.Translation
{
  public interface ITranslationPermissionService
  {
    Task<TranslationPermissionResponse> RequestPermissionAsync(RequestPermissionRequest dto);
    Task<TranslationPermissionResponse> ReviewPermissionAsync(int permissionId, ReviewPermissionRequest dto);
    Task<IEnumerable<TranslationPermissionResponse>> GetTeamPermissionsAsync(int teamId);
    Task<IEnumerable<TranslationPermissionResponse>> GetCreatorPermissionsAsync(int userId);
    Task<int> AutoDenyExpiredRequestsAsync(int expireHours = 72);
  }
}
