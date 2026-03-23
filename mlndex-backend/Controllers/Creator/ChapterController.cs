using Application.DTOs.Chapter;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Creator;

[ApiController]
[Route("api")]
[Authorize(Roles = "CREATOR,ADMIN")]
public class ChapterController : BaseController
{
  private readonly IChapterService _service;
  private readonly IMlndexDbContext _db;
  private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
  private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB per file

  public ChapterController(IChapterService service, IMlndexDbContext db)
  {
    _service = service;
    _db = db;
  }

  [HttpPost("creator/chapters/create")]
  [RequestSizeLimit(300 * 1024 * 1024)]
  public async Task<IActionResult> Create(
      [FromForm] int seriesId,
      [FromForm] float chapterNumber,
      [FromForm] string? title,
      [FromForm] int? languageId,
      [FromForm] IFormFileCollection pages,
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
    var dto = new CreateChapterDto
    {
      SeriesId = seriesId,
      ChapterNumber = chapterNumber,
      Title = title,
      LanguageId = languageId,
      TeamId = null,
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
  public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
  {
    try
    {
      var result = await _service.GetChapterDetailAsync(id, cancellationToken);
      if (result == null) return NotFoundResponse("Không tìm thấy chương truyện.");
      return OkResponse(result);
    }
    catch (Exception ex)
    {
      return ErrorResponse(ex.Message);
    }
  }


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

    var dto = new UpdateChapterDto
    {
      SeriesId = seriesId,
      ChapterNumber = chapterNumber,
      Title = title,
      LanguageId = languageId,
      RetainedPageIds = retainedPageIds,
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
