using Application.DTOs.Translation.Responses;
using Application.DTOs.Chapter;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.Financial
{
    public interface IContentUnlockService
    {
        Task<UnlockChapterResponseDto> UnlockChapterAsync(int userId, int chapterId, CancellationToken ct = default);
        Task<UnlockTranslationResponseDto> UnlockTranslationAsync(int userId, int translationId, CancellationToken ct = default);
    }
}
