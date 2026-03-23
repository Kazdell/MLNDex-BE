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

  [HttpPost("chapters/create")]
  [RequestSizeLimit(300 * 1024 * 1024)]
  public async Task<IActionResult> Create(
      [FromForm] int seriesId,
      [FromForm] float chapterNumber,
      [FromForm] string? title,
      [FromForm] int? languageId,
      [FromForm] string? language,
      [FromForm] int teamId,
      [FromForm] int? chapterId, // Base Chapter ID
      [FromForm] int? permissionId,
      [FromForm] string? creditsJson,
      [FromForm] string? jointTeamIdsJson,
      [FromForm] IFormFileCollection pages,
      CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    // Guard: Block upload if user trust score is depleted
    var currentUser = await _db.Users.FindAsync(userId);
    if (currentUser?.CannotUpload == true)
      return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

    // Resolve languageId from language name/code if not provided directly
    int? resolvedLanguageId = languageId;
    if (resolvedLanguageId == null && !string.IsNullOrEmpty(language))
    {
      var lang = await _db.Languages.FirstOrDefaultAsync(
          l => l.Name == language || l.Code == language, cancellationToken);
      resolvedLanguageId = lang?.LanguageId;
    }

    // 1. Validate files
    if (pages == null || pages.Count == 0)
      return BadRequestResponse("Chưa có trang nào được gửi lên.");

    foreach (var file in pages)
    {
      var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
      if (!AllowedExtensions.Contains(ext))
        return BadRequestResponse($"File '{file.FileName}' không hợp lệ. Chỉ chấp nhận: .jpg .jpeg .png .webp");

      if (file.Length > MaxFileSizeBytes)
        return BadRequestResponse($"File '{file.FileName}' vượt quá 20MB.");
    }

    // 2. Build DTO — Translation upload: TeamId is required
    var dto = new CreateChapterDto
    {
      SeriesId = seriesId,
      ChapterNumber = chapterNumber,
      Title = title,
      LanguageId = resolvedLanguageId,
      TeamId = teamId,
      BaseChapterId = chapterId,
      PermissionId = permissionId,
      CreditsJson = creditsJson,
      JointTeamIdsJson = jointTeamIdsJson,
      Pages = pages.Select((file, index) => new UploadPageDto
      {
        FileStream = file.OpenReadStream(),
        FileName = file.FileName,
        PageNumber = index + 1
      }).ToList()
    };

    try
    {
      var result = await _service.CreateAsync(userId, dto, cancellationToken);
      return OkResponse(result, "Đăng chương dịch thành công.");
    }
    catch (KeyNotFoundException ex)
    {
      return NotFoundResponse(ex.Message);
    }
    catch (UnauthorizedAccessException ex)
    {
      return UnauthorizedResponse(ex.Message);
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
