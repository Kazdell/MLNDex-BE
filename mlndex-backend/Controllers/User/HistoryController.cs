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
    private int CurrentUserId => int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");

    public HistoryController(IHistoryService historyService)
    {
      _historyService = historyService;
    }

    [HttpPost]
    public async Task<IActionResult> UpdateHistory([FromBody] ReadingHistoryUpdateDto dto, CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId == 0) return Unauthorized();

      var result = await _historyService.UpdateHistoryAsync(userId, dto, cancellationToken);
      return Ok(new { success = result, message = "Cập nhật lịch sử đọc thành công." });
    }

    [HttpGet]
    public async Task<IActionResult> GetUserHistory(CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId == 0) return Unauthorized();

      var history = await _historyService.GetUserHistoryAsync(userId, cancellationToken);
      return Ok(new { success = true, data = history });
    }

    [HttpDelete("{seriesId}")]
    public async Task<IActionResult> DeleteHistory(int seriesId, CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId == 0) return Unauthorized();

      var result = await _historyService.RemoveFromHistoryAsync(userId, seriesId, cancellationToken);
      return Ok(new { success = result, message = result ? "Đã xóa khỏi lịch sử." : "Không tìm thấy dữ liệu." });
    }

    [HttpDelete("all")]
    public async Task<IActionResult> ClearHistory(CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId == 0) return Unauthorized();

      var result = await _historyService.ClearAllHistoryAsync(userId, cancellationToken);
      return Ok(new { success = result, message = result ? "Đã xóa toàn bộ lịch sử." : "Lịch sử đã trống." });
    }
  }
}
