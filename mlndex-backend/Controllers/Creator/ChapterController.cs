using Application.DTOs.Chapter;
using Application.Interfaces.Creator;
using Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace mlndex_backend.Controllers.Creator;

[ApiController]
[Route("api/chapters")]
[Authorize(Roles = "CREATOR,ADMIN")]
public class ChapterController : BaseController
{
  private readonly IChapterService _service;
  private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
  private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB per file
  private int CurrentUserId => int.Parse(User.FindFirst("UserId")?.Value ?? "0");

        var userId = int.Parse(userIdClaim.Value); // Map UserId to CreatorId simplified for now

  [HttpPost]
  [RequestSizeLimit(300 * 1024 * 1024)]
  public async Task<IActionResult> Create(
      [FromForm] int seriesId,
      [FromForm] float chapterNumber,
      [FromForm] string? title,
      [FromForm] string? language,
      [FromForm] int? teamId,
      [FromForm] IFormFileCollection pages,
      CancellationToken cancellationToken)
  {
    var creatorId = CurrentUserId;
    if (creatorId == 0) return Unauthorized();
    // Kiểm tra tính hợp lệ của file
    if (pages.Count == 0)
      return BadRequest(new { message = "Chưa có trang nào được gửi lên." });

        var creatorId = creator.CreatorId;

        if (creatorId == 0) return Unauthorized();
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

    [AllowAnonymous]
    [HttpGet("/api/chapters/{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
      SeriesId = seriesId,
      ChapterNumber = chapterNumber,
      Title = title,
      Language = language,
      TeamId = teamId,
      Pages = pages.Select((file, index) => new UploadPageDto
      {
        FileStream = file.OpenReadStream(),
        FileName = file.FileName,
        PageNumber = index + 1
      }).ToList()
    };

        return Ok(result);
    }
}