using Application.DTOs.User;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Interfaces.User
{
  public interface IHistoryService
  {
    Task<bool> UpdateHistoryAsync(int userId, ReadingHistoryUpdateDto dto, CancellationToken cancellationToken = default);
    Task<List<ReadingHistoryResponseDto>> GetUserHistoryAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> RemoveFromHistoryAsync(int userId, int seriesId, CancellationToken cancellationToken = default);
    Task<bool> ClearAllHistoryAsync(int userId, CancellationToken cancellationToken = default);
    Task<ReadingStatsDto> GetReadingStatsAsync(int userId, CancellationToken cancellationToken = default);
  }
}
