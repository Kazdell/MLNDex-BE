using Application.DTOs.Chapter;
using Application.Interfaces;
using Application.Interfaces.Chapter;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlcdex_backend.Controllers;

[ApiController]
[Route("api/chapters/{chapterId:int}/pages")]
//[Authorize]
public class ChapterPagesController : ControllerBase
{
	private readonly IChapterPageService _service;

	private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
	private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20MB mỗi file

	public ChapterPagesController(IChapterPageService service)
		=> _service = service;

	/// <summary>
	/// Upload nhiều trang truyện cho một chapter.
	/// 
	/// Request: multipart/form-data
	/// Field name: "pages" (có thể gửi nhiều file cùng lúc)
	/// Thứ tự trang = thứ tự file trong request
	/// 
	/// Response trả về danh sách URL ảnh đã upload + kết quả kiểm duyệt AI
	/// </summary>
	[HttpPost]
	[RequestSizeLimit(300 * 1024 * 1024)] // tổng request tối đa 300MB
	public async Task<IActionResult> UploadPages(
		int chapterId,
		[FromForm] IFormFileCollection pages,
		CancellationToken cancellationToken)
	{
		// ── Validate ──────────────────────────────────────────────────
		if (pages.Count == 0)
			return BadRequest(new { message = "Chưa có file nào được gửi lên." });

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

		// ── Map IFormFile → UploadPageDto ─────────────────────────────
		var pageDtos = pages
			.Select((file, index) => new UploadPageDto
			{
				FileStream = file.OpenReadStream(),
				FileName = file.FileName,
				PageNumber = index + 1 // trang bắt đầu từ 1
			})
			.ToList();

		// ── Gọi Service ───────────────────────────────────────────────
		var result = await _service.UploadPagesAsync(chapterId, pageDtos, cancellationToken);

		return Ok(result);
	}

	/// <summary>Xóa một trang cụ thể.</summary>
	[HttpDelete("{pageId:int}")]
	public async Task<IActionResult> DeletePage(
		int pageId,
		CancellationToken cancellationToken)
	{
		await _service.DeletePageAsync(pageId, cancellationToken);
		return NoContent();
	}

	/// <summary>Xóa toàn bộ trang của chapter.</summary>
	[HttpDelete]
	public async Task<IActionResult> DeleteAllPages(
		int chapterId,
		CancellationToken cancellationToken)
	{
		await _service.DeleteAllPagesAsync(chapterId, cancellationToken);
		return NoContent();
	}
}