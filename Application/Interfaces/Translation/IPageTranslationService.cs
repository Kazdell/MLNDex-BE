using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
    public interface IPageTranslationService
    {
        Task<List<PageTextLayerDto>> GetPageTextLayerAsync(int pageId);
        Task<List<PageTextLayerDto>> GeneratePageTextLayerAsync(int pageId, string targetLanguage = "Vietnamese");
    }
}
