using Application.DTOs.Chapter;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Chapter;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Mlndex.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Services.Chapter
{
	/// Orchestrator điều phối toàn bộ luồng upload trang truyện:
	/// 1. Upload ảnh lên Cloudinary → nhận URL
	/// 2. Lưu URL vào bảng ChapterPages trong DB
	/// 3. Gửi URL cho AI kiểm duyệt → cập nhật moderation_status
	public class ChapterPageService : IChapterPageService
	{
		private readonly IStorageService _storage;
		private readonly MlndexDbContext _db;
		private readonly ILogger<ChapterPageService> _logger;

		public ChapterPageService(
			IStorageService storage,
			MlndexDbContext db,
			ILogger<ChapterPageService> logger)
		{
			_storage = storage;
			_db = db;
			_logger = logger;
		}

		public async Task<UploadChapterPagesResponseDto> UploadPagesAsync(
		int chapterId,
		List<UploadPageDto> pages,
		CancellationToken cancellationToken = default)
		{
			// ── 1. Kiểm tra chapter tồn tại ──────────────────────────────
			var chapter = await _db.Chapters
				.FindAsync([chapterId], cancellationToken)
				?? throw new KeyNotFoundException($"Chapter {chapterId} không tồn tại.");

			// ── 2. Tính PageNumber tiếp nối từ trang cuối cùng đã có ─────
			var lastPageNumber = await _db.ChapterPages
				.Where(p => p.ChapterId == chapterId)
				.OrderByDescending(p => p.PageNumber)
				.Select(p => (int?)p.PageNumber)
				.FirstOrDefaultAsync(cancellationToken) ?? 0;

			// ── 3. Upload từng ảnh lên Cloudinary + lưu URL vào DB ────────
			var results = new List<ChapterPageResponseDto>();
			var folder = $"chapters/{chapterId}/pages";
			var pageIndex = 0;

			foreach (var page in pages.OrderBy(p => p.PageNumber))
			{
				pageIndex++;
				var newPageNumber = lastPageNumber + pageIndex;

				var imageUrl = await _storage.UploadAsync(
					page.FileStream,
					page.FileName,
					folder,
					cancellationToken);

				var entity = new ChapterPage
				{
					ChapterId = chapterId,
					PageNumber = newPageNumber,
					ImageUrl = imageUrl
				};

				_db.ChapterPages.Add(entity);
				await _db.SaveChangesAsync(cancellationToken);

				results.Add(new ChapterPageResponseDto
				{
					PageId = entity.PageId,
					ChapterId = chapterId,
					PageNumber = newPageNumber,
					ImageUrl = imageUrl
				});
			}

			// ── 4. Đặt chapter về PENDING — chờ AI kiểm duyệt ───────────
			chapter.ModerationStatus = ModerationStatus.PENDING;
			await _db.SaveChangesAsync(cancellationToken);

			_logger.LogInformation(
				"Upload xong {Count} trang cho chapter {ChapterId}. Trang {From} đến {To}. Đang chờ kiểm duyệt.",
				results.Count, chapterId, lastPageNumber + 1, lastPageNumber + results.Count);

			return new UploadChapterPagesResponseDto
			{
				ChapterId = chapterId,
				TotalPages = results.Count,
				Pages = results,
				ModerationStatus = chapter.ModerationStatus.ToString()
			};
		}

		public async Task DeletePageAsync(int pageId, CancellationToken cancellationToken = default)
		{
			var page = await _db.ChapterPages
				.FindAsync([pageId], cancellationToken)
				?? throw new KeyNotFoundException($"Page {pageId} không tồn tại.");

			await _storage.DeleteAsync(page.ImageUrl, cancellationToken);
			_db.ChapterPages.Remove(page);
			await _db.SaveChangesAsync(cancellationToken);
		}

		public async Task DeleteAllPagesAsync(int chapterId, CancellationToken cancellationToken = default)
		{
			await _storage.DeleteFolderAsync($"chapters/{chapterId}/pages", cancellationToken);

			var pages = await _db.ChapterPages
				.Where(p => p.ChapterId == chapterId)
				.ToListAsync(cancellationToken);

			_db.ChapterPages.RemoveRange(pages);
			await _db.SaveChangesAsync(cancellationToken);
		}
	}
}
