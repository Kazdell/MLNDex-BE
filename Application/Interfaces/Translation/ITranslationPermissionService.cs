using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
    public interface ITranslationPermissionService
    {
        Task<TranslationPermissionDto> RequestPermissionAsync(RequestPermissionDto dto);
        Task<TranslationPermissionDto> ReviewPermissionAsync(int permissionId, ReviewPermissionDto dto);
    }
}
