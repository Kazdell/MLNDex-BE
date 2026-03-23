using Application.DTOs.User;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.User
{
  public interface IBookmarkService
  {
    Task<BookmarkResponseDto> UpsertBookmarkAsync(int userId, BookmarkRequestDto dto, CancellationToken ct = default);
    Task<List<BookmarkResponseDto>> GetUserBookmarksAsync(int userId, CancellationToken ct = default);
    Task<BookmarkResponseDto?> GetBookmarkForSeriesAsync(int userId, int seriesId, CancellationToken ct = default);
    Task<bool> DeleteBookmarkAsync(int userId, int bookmarkId, CancellationToken ct = default);
  }
}
