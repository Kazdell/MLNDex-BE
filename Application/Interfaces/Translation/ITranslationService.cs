using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;

namespace Application.Interfaces.Translation
{
  public interface ITranslationService
  {
    Task<TranslationResponse> UploadTranslationAsync(UploadTranslationRequest dto);
    Task<TranslationResponse?> GetTranslationByIdAsync(int translationId);
    Task<IEnumerable<TranslationResponse>> GetTranslationsBySeriesAsync(int seriesId);
    Task<IEnumerable<TranslationResponse>> GetAllTranslationsAsync();
    Task<TranslationResponse> EditTranslationAsync(int translationId, EditTranslationRequest dto);
    Task<bool> DeleteTranslationAsync(int translationId);
    Task<List<Application.DTOs.Chapter.ChapterListItemDto>> GetTeamTranslationsBySeriesAsync(int teamId, int seriesId, int userId, global::System.Threading.CancellationToken ct = default);
    Task<bool> DeleteTeamTranslationAsync(int translationId, int teamId, int userId, global::System.Threading.CancellationToken ct = default);
    Task<UnlockTranslationResponseDto> UnlockAsync(
        int userId, int translationId, CancellationToken ct = default);
  }
}
