using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
    public interface ITranslationPermissionService
    {
        Task<TranslationPermissionDto> RequestPermissionAsync(int requesterId, RequestPermissionDto dto);
        Task<TranslationPermissionDto> ReviewPermissionAsync(int permissionId, int creatorId, ReviewPermissionDto dto);
    }
}
