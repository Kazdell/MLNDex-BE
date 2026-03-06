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
  }
}
