using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
    public interface IReaderTranslationService
    {
        Task<List<OverlayTranslationResponse>> GenerateOverlayTranslationsAsync(OverlayTranslationRequest request);
    }
}
