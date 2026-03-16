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

    Task<ChapterDetailDto?> GetForEditAsync(
        int chapterId,
        int userId,
        CancellationToken cancellationToken = default);

    Task<CreateChapterResponseDto> UpdateAsync(
        int chapterId,
        int userId,
        UpdateChapterDto dto,
        Microsoft.AspNetCore.Http.IFormFileCollection? newPages,
        CancellationToken cancellationToken = default);

    Task<ModerationStatusDto> GetModerationStatusAsync(int chapterId);

    Task RetryModerationAsync(int chapterId, int userId);
  }
}
