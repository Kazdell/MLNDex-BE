using Application.DTOs.User;
using Application.Interfaces.User;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace mlndex_backend.Controllers.User
{
  [ApiController]
  [Route("api/user/{userId:int}/history")]
  public class HistoryController : ControllerBase
  {
    private readonly IHistoryService _historyService;

    public HistoryController(IHistoryService historyService)
    {
      _historyService = historyService;
    }

    [HttpPost]
    public async Task<IActionResult> UpdateHistory(int userId, [FromBody] ReadingHistoryUpdateDto dto, CancellationToken cancellationToken)
    {
      var result = await _historyService.UpdateHistoryAsync(userId, dto, cancellationToken);
      return Ok(new { success = result, message = "Cập nhật lịch sử đọc thành công." });
    }

    [HttpGet]
    public async Task<IActionResult> GetUserHistory(int userId, CancellationToken cancellationToken)
    {
      var history = await _historyService.GetUserHistoryAsync(userId, cancellationToken);
      return Ok(new { success = true, data = history });
    }
  }
}
