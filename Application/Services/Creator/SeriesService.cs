using Application.DTOs.Creator;
using Application.Interfaces;
using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Creator
{
    public class SeriesService : ISeriesService
    {
        private readonly IMlndexDbContext _context;
        private readonly IStorageService _storage;
        private readonly ILogger<SeriesService> _logger;

        public SeriesService(
            IMlndexDbContext context,
            IStorageService storage,
            ILogger<SeriesService> logger)
        {
            _context = context;
            _storage = storage;
            _logger = logger;
        }

        public async Task<CreateSeriesResponseDto> CreateAsync(
            int creatorId,
            CreateSeriesDto dto,
            CancellationToken cancellationToken = default)
        {
            // ── 1. Kiểm tra creator tồn tại ──────────────────────────────
            var creator = await _context.CreatorProfiles
                .FindAsync([creatorId], cancellationToken)
                ?? throw new KeyNotFoundException($"Creator {creatorId} không tồn tại.");

            // ── 2. Upload ảnh bìa lên Cloudinary ─────────────────────────
            string? imageUrl = null;
            if (dto.CoverImage != null)
            {
                imageUrl = await _storage.UploadAsync(
                    dto.CoverImage.OpenReadStream(),
                    dto.CoverImage.FileName,
                    "covers/novels",
                    cancellationToken);
            }

            try
            {
                // ── 3. Tính AgeRating từ content scores ───────────────────
                var maxScore = new[] { dto.Violence, dto.Nudity, dto.SexualContent }.Max();
                var ageRating = maxScore switch
                {
                    >= 3 => AgeRating.ADULT,
                    >= 2 => AgeRating.MATURE,
                    >= 1 => AgeRating.TEEN,
                    _ => AgeRating.ALL
                };

                // ── 4. Build và lưu entity ────────────────────────────────
                var series = new Series
                {
                    CreatorId = creatorId,
                    Title = dto.Title,
                    Description = dto.Description,
                    CoverImageUrl = imageUrl,
                    SeriesFormat = SeriesFormat.NOVEL,
                    AgeRating = ageRating,
                    Status = SeriesStatus.ONGOING,
                    ModerationStatus = ModerationStatus.PENDING,
                    AverageRating = 0,
                    TotalRatings = 0,
                    CreatedAt = DateTime.UtcNow,
                };

                _context.Series.Add(series);
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Tạo novel thành công. SeriesId: {SeriesId}, Title: {Title}, CreatorId: {CreatorId}.",
                    series.SeriesId, series.Title, creatorId);

                return new CreateSeriesResponseDto
                {
                    SeriesId = series.SeriesId,
                    Title = series.Title,
                    CoverImageUrl = series.CoverImageUrl,
                    AgeRating = series.AgeRating.ToString(),
                    ModerationStatus = series.ModerationStatus.ToString()
                };
            }
            catch (Exception ex)
            {
                // ── 5. Cleanup: Xóa ảnh đã upload nếu DB lỗi ─────────────
                if (imageUrl != null)
                {
                    _logger.LogWarning(ex,
                        "Lưu DB thất bại. Đang xóa ảnh đã upload: {ImageUrl}", imageUrl);
                    await _storage.DeleteAsync(imageUrl, cancellationToken);
                }
                throw;
            }
        }
    }
}