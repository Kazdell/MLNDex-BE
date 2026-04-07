using Application.Interfaces.Creator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;

namespace mlndex_backend.Controllers.Moderation;

/// <summary>
/// Public moderation-status endpoints — accessible by ANY authenticated user
/// (creators, translation team members, admins).
/// Kept separate from ChapterController to avoid class-level [Authorize(Roles="CREATOR,ADMIN")].
/// </summary>
[ApiController]
[Route("api")]
[Authorize] // any authenticated user, no role restriction
public class ChapterModerationStatusController : BaseController
{
  private readonly IChapterService _service;

  public ChapterModerationStatusController(IChapterService service)
  {
    _service = service;
  }

  /// <summary>
  /// Returns the current AI moderation status for a chapter.
  /// Called by both creators and translation team members from ModerationResult page.
  /// </summary>
  [HttpGet("chapters/{chapterId:int}/moderation-status")]
  public async Task<IActionResult> GetModerationStatus(
      int chapterId, CancellationToken cancellationToken)
  {
    try
    {
      var result = await _service.GetModerationStatusAsync(chapterId, cancellationToken);
      return OkResponse(result);
    }
    catch (Exception ex)
    {
      return ErrorResponse(ex.Message);
    }
  }

  /// <summary>
  /// Enqueues a chapter for AI moderation re-evaluation.
  /// </summary>
  [HttpPost("chapters/{chapterId:int}/moderation-retry")]
  public async Task<IActionResult> RetryModeration(
      int chapterId, CancellationToken cancellationToken)
  {
    try
    {
      await _service.RetryModerationAsync(chapterId, cancellationToken);
      return OkResponse<object?>(null, "Đã đưa chapter vào hàng đợi kiểm duyệt lại.");
    }

    catch (InvalidOperationException ex)
    {
      return BadRequestResponse(ex.Message);
    }
    catch (Exception ex)
    {
      return ErrorResponse(ex.Message);
    }
  }
}
