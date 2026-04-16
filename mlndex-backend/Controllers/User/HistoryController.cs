using Application.DTOs.Common;
using Application.Exceptions;
using Application.DTOs.User;
using Application.Interfaces.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace mlndex_backend.Controllers.User
{
  [ApiController]
  [Route("api/user/history")]
  [Authorize]
  public class HistoryController : BaseController
  {
    private readonly IHistoryService _historyService;
    private int CurrentUserId => GetUserId();

    public HistoryController(IHistoryService historyService)
    {
      _historyService = historyService;
    }

    [HttpPost]
    public async Task<IActionResult> UpdateHistory([FromBody] ReadingHistoryUpdateDto dto, CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId < 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var result = await _historyService.UpdateHistoryAsync(userId, dto, cancellationToken);
      return OkResponse(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetUserHistory(CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId < 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var history = await _historyService.GetUserHistoryAsync(userId, cancellationToken);
      return OkResponse(history);
    }

    [HttpDelete("{seriesId}")]
    public async Task<IActionResult> DeleteHistory(int seriesId, CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId < 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var result = await _historyService.RemoveFromHistoryAsync(userId, seriesId, cancellationToken);
      return OkResponse(result);
    }

    [HttpDelete("all")]
    public async Task<IActionResult> ClearHistory(CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId < 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var result = await _historyService.ClearAllHistoryAsync(userId, cancellationToken);
      return OkResponse(result);
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetReadingStats(CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId < 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var stats = await _historyService.GetReadingStatsAsync(userId, cancellationToken);
      return OkResponse(stats);
    }
  }
}
