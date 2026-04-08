using Application.DTOs.Chapter;
using Application.Interfaces.Creator;
using Application.Interfaces.Financial;
using Application.Interfaces.Data;
using Application.Services.Creator;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace mlndex_backend.Controllers.Creator;

[ApiController]
[Route("api")]
[Authorize(Roles = "CREATOR,ADMIN,READER")]
public class ChapterController : BaseController
{
  private readonly IChapterService _service;
  private readonly IContentUnlockService _contentUnlockService;
  private readonly IMlndexDbContext _db;
  private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
  private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB per file

  private readonly Application.Interfaces.AIModeration.IModerationService _moderationService;
  public ChapterController(IChapterService service, IContentUnlockService contentUnlockService, Application.Interfaces.AIModeration.IModerationService moderationService, IMlndexDbContext db)
  {
    _service = service;
    _contentUnlockService = contentUnlockService;
    _moderationService = moderationService;
    _db = db;
  }
  [Authorize(Roles = "CREATOR,ADMIN")]
  [HttpPost("creator/chapters/create")]
  [RequestSizeLimit(300 * 1024 * 1024)]
  public async Task<IActionResult> Create(
      [FromForm] int seriesId,
      [FromForm] float chapterNumber,
      [FromForm] string? title,
      [FromForm] int? languageId,
      [FromForm] IFormFileCollection pages,
      [FromForm] string? lockStatus,
      [FromForm] int? unlockPriceCoins,
      [FromForm] int? freeAfterDays,
      CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    var currentUser = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
    if (currentUser?.CannotUpload == true)
      return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

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

    // 2. Build DTO — Creator upload: no TeamId
    ChapterLockStatus? parsedLock = null;
    if (!string.IsNullOrEmpty(lockStatus) &&
        Enum.TryParse<ChapterLockStatus>(lockStatus, true, out var lockEnum))
      parsedLock = lockEnum;

    var dto = new CreateChapterDto
    {
      SeriesId = seriesId,
      ChapterNumber = chapterNumber,
      Title = title,
      LanguageId = languageId,
      TeamId = null,
      LockStatus = parsedLock,
      UnlockPriceCoins = unlockPriceCoins,
      FreeAfterDays = freeAfterDays,
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
      return OkResponse(result, "Đăng chương truyện thành công.");
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

  [AllowAnonymous]
  [HttpGet("chapters/{id:int}")]
    public async Task<IActionResult> GetById(
    int id,
    [FromQuery] int? translationId, // ASP.NET tự động lấy từ ?translationId=...
    CancellationToken cancellationToken)
  {
        // Lấy UserId từ Claims (giữ nguyên logic của bạn)
    int? userId = null;
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (int.TryParse(claim, out var parsed)) userId = parsed;


    try
    {
      var result = await _service.GetChapterDetailAsync(
          id,
          userId,
          translationId,                  // int? — có thể null
          cancellationToken);             // named arg tránh nhầm slot
      if (result == null) return NotFoundResponse("Không tìm thấy chương này.");
      return OkResponse(result);
    }
    catch (Exception ex)
    {
      return ErrorResponse(ex.Message);
    }
  }

  [Authorize(Roles = "CREATOR,ADMIN")]
  [HttpGet("creator/series/{seriesId:int}/chapters")]
  public async Task<IActionResult> GetBySeries(int seriesId, CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    try
    {
      var result = await _service.GetBySeriesAsync(seriesId, userId, cancellationToken);
      return OkResponse(result);
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

  [Authorize(Roles = "CREATOR,ADMIN")]
  [HttpGet("creator/chapters/{id:int}/edit")]
  public async Task<IActionResult> GetForEdit(int id, CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    try
    {
      var result = await _service.GetForEditAsync(id, userId, cancellationToken);
      if (result == null) return NotFoundResponse("Không tìm thấy chương truyện hoặc bạn không có quyền chỉnh sửa.");
      return OkResponse(result);
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

  [Authorize(Roles = "CREATOR,ADMIN")]
  [HttpPut("creator/chapters/{id:int}")]
  [RequestSizeLimit(300 * 1024 * 1024)]
  public async Task<IActionResult> Update(
      int id,
      [FromForm] int seriesId,
      [FromForm] float chapterNumber,
      [FromForm] string? title,
      [FromForm] int? languageId,
      [FromForm] int? teamId,
      [FromForm] string? retainedPageIds,
      [FromForm] IFormFileCollection? pages,
      [FromForm] string? lockStatus,
      [FromForm] int? unlockPriceCoins,
      [FromForm] int? freeAfterDays,
      CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    var currentUser = await _db.Users.FindAsync(new object[] { userId }, cancellationToken);
    if (currentUser?.CannotUpload == true)
      return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

    // Validate new files if provided
    if (pages != null && pages.Count > 0)
    {
      foreach (var file in pages)
      {
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
          return BadRequestResponse($"File '{file.FileName}' không hợp lệ. Chỉ chấp nhận: .jpg .jpeg .png .webp");
        if (file.Length > MaxFileSizeBytes)
          return BadRequestResponse($"File '{file.FileName}' vượt quá 20MB.");
      }
    }

    ChapterLockStatus? parsedLockStatus = null;
    if (!string.IsNullOrEmpty(lockStatus) &&
        Enum.TryParse<ChapterLockStatus>(lockStatus, ignoreCase: true, out var lockEnum))
    {
      parsedLockStatus = lockEnum;
    }

    var dto = new UpdateChapterDto
    {
      SeriesId = seriesId,
      ChapterNumber = chapterNumber,
      Title = title,
      LanguageId = languageId,
      RetainedPageIds = retainedPageIds,
      // ✅ Gán vào DTO
      LockStatus = parsedLockStatus,
      UnlockPriceCoins = unlockPriceCoins,
      FreeAfterDays = freeAfterDays,
    };

    var newPages = pages?.Select((file, index) => new UploadPageDto
    {
      FileStream = file.OpenReadStream(),
      FileName = file.FileName,
      PageNumber = index + 1
    }).ToList();

    try
    {
      var result = await _service.UpdateAsync(id, userId, dto, newPages, cancellationToken);
      return OkResponse(result, "Cập nhật chương thành công.");
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

  [Authorize(Roles = "CREATOR,ADMIN")]
  [HttpPatch("creator/chapters/{chapterId:int}/lock")]
  public async Task<IActionResult> UpdateLockStatus(
  int chapterId,
  [FromBody] UpdateChapterLockDto dto,
  CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    try
    {
      var result = await _service.UpdateChapterLockStatusAsync(chapterId, userId, dto, cancellationToken);
      return OkResponse(result, "Cập nhật trạng thái khóa thành công.");
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

  [HttpPost("chapters/{chapterId:int}/unlock")]
  public async Task<IActionResult> Unlock(int chapterId, CancellationToken ct)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Vui lòng đăng nhập.");

    try
    {
      var result = await _contentUnlockService.UnlockChapterAsync(userId, chapterId, ct);
      return OkResponse(result, "Mở khóa chương thành công.");
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

  [HttpDelete("creator/chapters/{id:int}")]
  public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
  {
    int userId = GetUserId();
    if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

    try
    {
      await _service.DeleteAsync(id, userId, cancellationToken);
      return OkResponse((object?)null, "Xóa chương thành công.");
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
