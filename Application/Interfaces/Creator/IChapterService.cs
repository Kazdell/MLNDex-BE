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
int? userId,
int? translationId = null,
CancellationToken cancellationToken = default);
    Task<List<ChapterListItemDto>> GetBySeriesAsync(int seriesId, int userId, CancellationToken ct = default);
    Task<List<ChapterListItemDto>> GetTeamChaptersBySeriesAsync(int teamId, int seriesId, int userId, CancellationToken ct = default);
    Task<ChapterDetailDto?> GetForEditAsync(int chapterId, int userId, CancellationToken ct = default);
    Task<CreateChapterResponseDto> UpdateAsync(int chapterId, int userId, UpdateChapterDto dto, List<UploadPageDto>? newPages, CancellationToken ct = default);

    Task<UpdateChapterLockResponseDto> UpdateChapterLockStatusAsync(int chapterId, int requestingUserId, UpdateChapterLockDto dto, CancellationToken ct = default);

    Task DeleteAsync(int chapterId, int userId, CancellationToken ct = default);
    Task DeleteTranslationChapterAsync(int chapterId, int teamId, int userId, CancellationToken ct = default);
  }
}
