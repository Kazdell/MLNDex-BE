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
        Task<ChapterDetailDto?> GetChapterDetailAsync(int chapterId, int? userId = null, CancellationToken ct = default);
        Task<ChapterModerationStatusDto> GetModerationStatusAsync(int chapterId, CancellationToken ct = default);
        Task RetryModerationAsync(int chapterId, CancellationToken ct = default);
        Task<List<ChapterListItemDto>> GetBySeriesAsync(int seriesId, int userId, CancellationToken ct = default);
        Task<ChapterDetailDto?> GetForEditAsync(int chapterId, int userId, CancellationToken ct = default);
        Task<CreateChapterResponseDto> UpdateAsync(int chapterId, int userId, UpdateChapterDto dto, List<UploadPageDto>? newPages, CancellationToken ct = default);

        Task<UpdateChapterLockResponseDto> UpdateChapterLockStatusAsync(int chapterId, int requestingUserId, UpdateChapterLockDto dto, CancellationToken ct = default);
        Task<UnlockChapterResponseDto> UnlockAsync(
        int userId, int chapterId, CancellationToken ct = default);
    }
}
