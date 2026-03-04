using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Mlndex.Data;
using Application.DTOs.Creator;

namespace mlndex_backend.Controllers.Creator
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeriesController : ControllerBase
    {
        private readonly MlndexDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SeriesController(MlndexDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // ... Keep your GET/PUT/DELETE methods ...

        // POST: api/Series/create
        [HttpPost("create")]
        public async Task<IActionResult> Create([FromForm] CreateSeriesDto dto)
        {
            string? imageUrl = null;

            // We use a try-catch to ensure we can clean up files if SaveChangesAsync fails
            try
            {
                // 1. Save images to disk
                if (dto.CoverImage != null)
                    imageUrl = await SaveImageAsync(dto.CoverImage, "covers/images");

                // 2. Logic for AgeRating
                var maxScore = new[] { dto.Violence, dto.Nudity, dto.SexualContent }.Max();
                var ageRating = maxScore switch
                {
                    >= 3 => AgeRating.ADULT,
                    >= 2 => AgeRating.MATURE,
                    >= 1 => AgeRating.TEEN,
                    _ => AgeRating.ALL
                };

                // 3. Build entity
                var series = new Series
                {
                    CreatorId = 1,
                    Title = dto.Title,
                    Description = dto.Description,
                    CoverImageUrl = imageUrl,
                    SeriesFormat = SeriesFormat.MANGA,
                    AgeRating = ageRating,
                    Status = SeriesStatus.ONGOING,
                    ModerationStatus = ModerationStatus.PENDING,
                    AverageRating = 0,
                    TotalRatings = 0,
                    CreatedAt = DateTime.UtcNow,
                };

                _context.Series.Add(series);

                // This is where the "Truncation" or "Conflict" error happens
                await _context.SaveChangesAsync();

                return Ok(new { series.SeriesId, series.Title });
            }
            catch (Exception)
            {
                // 4. CLEANUP: If the DB save fails, delete the images we just saved
                DeleteLocalFile(imageUrl);

                // Re-throw or return the error so you can see it in the log
                throw;
            }
        }

        private async Task<string> SaveImageAsync(IFormFile file, string folder)
        {
            var uploads = Path.Combine(_env.WebRootPath, folder);
            Directory.CreateDirectory(uploads);

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploads, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/{folder}/{fileName}";
        }

        private void DeleteLocalFile(string? relativeUrl)
        {
            if (string.IsNullOrEmpty(relativeUrl)) return;

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativeUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
