using Application.DTOs.Chapter;
using Application.Interfaces.Creator;
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
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB per file

    public ChapterController(IChapterService service)
    {
        _service = service;
    }

    [HttpPost("creator/chapters/create")]
    [RequestSizeLimit(300 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        [FromForm] int seriesId,
        [FromForm] float chapterNumber,
        [FromForm] string? title,
        [FromForm] int? languageId,
        [FromForm] int? teamId,
        [FromForm] IFormFileCollection pages,
        CancellationToken cancellationToken)
    {
        int userId = GetUserId();
        if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

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

        // 2. Build DTO
        var dto = new CreateChapterDto
        {
            SeriesId = seriesId,
            ChapterNumber = chapterNumber,
            Title = title,
            LanguageId = languageId,
            TeamId = teamId,
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

    [HttpPost("chapters/{chapterId:int}/moderation-retry")]
    public async Task<IActionResult> RetryModeration(
        int chapterId, CancellationToken cancellationToken)
    {
        try
        {
            await _service.RetryModerationAsync(chapterId, cancellationToken);
            return OkResponse<object>(null, "Đã đưa chapter vào hàng đợi kiểm duyệt lại.");
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResponse(ex.Message);
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