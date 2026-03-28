using Application.DTOs.Chapter;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace mlndex_backend.Controllers.Translation;

/// <summary>
/// Handles chapter uploads from Translation Teams.
/// Separated from Creator's ChapterController to enforce proper role boundaries.
/// </summary>
[ApiController]
[Route("api/translation")]
[Authorize] // Any authenticated user — service layer checks team membership + permissions
public class TranslationChapterController : BaseController
{
  private readonly IChapterService _service;
  private readonly IMlndexDbContext _db;
  private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
  private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB per file

  public TranslationChapterController(IChapterService service, IMlndexDbContext db)
  {
    _service = service;
    _db = db;
  }


  [HttpGet("teams/{teamId:int}/series/{seriesId:int}/chapters")]
  public async Task<IActionResult> GetTeamChaptersBySeries(int teamId, int seriesId, CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    try
    {
      var result = await _service.GetTeamChaptersBySeriesAsync(teamId, seriesId, userId, cancellationToken);
      return OkResponse(result);
    }
    catch (KeyNotFoundException ex)
    {
      return NotFoundResponse(ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
      return UnauthorizedResponse(ex.Message);
    }
    catch (Exception ex)
    {
      return ErrorResponse(ex.Message);
    }
  }

  [HttpDelete("teams/{teamId:int}/chapters/{id:int}")]
  public async Task<IActionResult> DeleteTranslationChapter(int teamId, int id, CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    try
    {
      await _service.DeleteTranslationChapterAsync(id, teamId, userId, cancellationToken);
      return OkResponse((object?)null, "Xóa chương dịch thành công.");
    }
    catch (KeyNotFoundException ex)
    {
      return NotFoundResponse(ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
      return UnauthorizedResponse(ex.Message);
    }
    catch (Exception ex)
    {
      return ErrorResponse(ex.Message);
    }
  }
}
