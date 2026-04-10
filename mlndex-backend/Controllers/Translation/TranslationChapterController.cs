using Application.DTOs.Common;
using Application.Exceptions;
using Application.DTOs.Chapter;
using Application.Interfaces.Translation;
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
  private readonly ITranslationService _service;
  private readonly IMlndexDbContext _db;
  private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
  private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB per file

  public TranslationChapterController(ITranslationService service, IMlndexDbContext db)
  {
    _service = service;
    _db = db;
  }


  [HttpGet("teams/{teamId:int}/series/{seriesId:int}/chapters")]
  public async Task<IActionResult> GetTeamChaptersBySeries(int teamId, int seriesId, CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var result = await _service.GetTeamTranslationsBySeriesAsync(teamId, seriesId, userId, cancellationToken);
      return OkResponse(result);
  }

  [HttpDelete("teams/{teamId:int}/chapters/{id:int}")]
  public async Task<IActionResult> DeleteTranslationChapter(int teamId, int id, CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      await _service.DeleteTeamTranslationAsync(id, teamId, userId, cancellationToken);
      return OkResponse((object?)null, "Xóa chương dịch thành công.");
  }
}
