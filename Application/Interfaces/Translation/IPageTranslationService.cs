using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.User;

namespace Application.Interfaces.Translation
{
    public interface IPageTranslationService
    {
        Task<List<PageTextLayerResponse>> GetPageTextLayerAsync(int pageId);
        Task<List<PageTextLayerResponse>> GeneratePageTextLayerAsync(int pageId, string targetLanguage = "Vietnamese");
    }
}
