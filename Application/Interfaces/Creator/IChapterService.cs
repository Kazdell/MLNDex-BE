using Application.DTOs.AIModeration;
using Application.DTOs.Chapter;

namespace Application.Interfaces.Creator
{
  public interface IChapterService
  {
    Task<CreateChapterResponseDto> CreateAsync(
        int userId,
        CreateChapterDto dto,
        CancellationToken cancellationToken = default);
        Task<ChapterDetailDto?> GetChapterDetailAsync(
        int chapterId,
        CancellationToken cancellationToken = default);
        Task<ChapterModerationStatusDto> GetModerationStatusAsync(int chapterId, CancellationToken ct = default);
        Task RetryModerationAsync(int chapterId, CancellationToken ct = default);
    }
}
