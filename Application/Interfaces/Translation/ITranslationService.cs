using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;

namespace Application.Interfaces.Translation
{
    public interface ITranslationService
    {
        Task<TranslationDto> UploadTranslationAsync(int uploaderId, UploadTranslationDto dto);
        Task<TranslationDto?> GetTranslationByIdAsync(int translationId);
        Task<IEnumerable<TranslationDto>> GetTranslationsBySeriesAsync(int seriesId);
        Task<IEnumerable<TranslationDto>> GetAllTranslationsAsync();
        Task<TranslationDto> EditTranslationAsync(int translationId, int uploaderId, EditTranslationDto dto);
        Task<bool> DeleteTranslationAsync(int translationId, int uploaderId);
    }
}
