using Application.DTOs.Chapter;
using Application.Interfaces.Creator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace mlndex_backend.Controllers.Creator;

[ApiController]
[Route("api/creator/{creatorId:int}/chapters")]
public class ChapterController : ControllerBase
{
    private readonly IChapterService _service;
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB per file

    public ChapterController(IChapterService service) => _service = service;

    [HttpPost]
    [RequestSizeLimit(300 * 1024 * 1024)]
    public async Task<IActionResult> Create(
        int creatorId,
        [FromForm] int seriesId,
        [FromForm] float chapterNumber,
        [FromForm] string? title,
        [FromForm] string? language,
        [FromForm] IFormFileCollection pages,
        CancellationToken cancellationToken)
    {
        // Kiểm tra tính hợp lệ của file
        if (pages.Count == 0)
            return BadRequest(new { message = "Chưa có trang nào được gửi lên." });

        foreach (var file in pages)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
                return BadRequest(new
                {
                    message = $"File '{file.FileName}' không hợp lệ. Chỉ chấp nhận: .jpg .jpeg .png .webp"
                });

            if (file.Length > MaxFileSizeBytes)
                return BadRequest(new
                {
                    message = $"File '{file.FileName}' vượt quá 20MB."
                });
        }

        // Chuyển đổi dữ liệu sang DTO
        var dto = new CreateChapterDto
        {
            SeriesId = seriesId,
            ChapterNumber = chapterNumber,
            Title = title,
            Language = language,
            Pages = pages.Select((file, index) => new UploadPageDto
            {
                FileStream = file.OpenReadStream(),
                FileName = file.FileName,
                PageNumber = index + 1
            }).ToList()
        };

        // Gọi Service xử lý nghiệp vụ
        var result = await _service.CreateAsync(creatorId, dto, cancellationToken);
        return CreatedAtAction(nameof(Create), new { id = result.ChapterId }, result);
    }
}