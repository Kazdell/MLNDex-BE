using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
  public interface ITranslationPermissionService
  {
    Task<TranslationPermissionDto> RequestPermissionAsync(RequestPermissionDto dto);
    Task<TranslationPermissionDto> ReviewPermissionAsync(int permissionId, ReviewPermissionDto dto);
    Task<IEnumerable<TranslationPermissionDto>> GetTeamPermissionsAsync(int teamId);
    Task<IEnumerable<TranslationPermissionDto>> GetCreatorPermissionsAsync(int userId);
    Task<int> AutoDenyExpiredRequestsAsync(int expireHours = 72);
  }
}
