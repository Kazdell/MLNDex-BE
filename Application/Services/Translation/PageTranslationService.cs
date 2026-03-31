using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Application.Interfaces.Translation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Translation
{
    [Obsolete("Use ReaderTranslationService instead. Will be removed in Phase 3.")]
    public class PageTranslationService : IPageTranslationService
    {
        private readonly IMlndexDbContext _context;
        private readonly IOCRService _ocrService;
        private readonly IAiTranslationClient _aiClient;
        private readonly ILogger<PageTranslationService> _logger;
        private readonly HttpClient _httpClient;

        public PageTranslationService(
            IMlndexDbContext context,
            IOCRService ocrService,
            IAiTranslationClient aiClient,
            ILogger<PageTranslationService> logger,
            HttpClient httpClient)
        {
            _context = context;
            _ocrService = ocrService;
            _aiClient = aiClient;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<List<PageTextLayerDto>> GetPageTextLayerAsync(int pageId)
        {
            var layers = await _context.PageTextLayers
                .Where(l => l.PageId == pageId)
                .ToListAsync();

            return layers.Select(l => new PageTextLayerDto
            {
                LayerId = l.LayerId,
                PageId = l.PageId,
                X = l.X,
                Y = l.Y,
                Width = l.Width,
                Height = l.Height,
                OriginalText = l.OriginalText,
                TranslatedText = l.TranslatedText,
                IsVerified = l.IsVerified,
                SourceLanguage = l.SourceLanguage,
                TargetLanguage = l.TargetLanguage,
                TranslationProvider = l.TranslationProvider
            }).ToList();
        }

        public async Task<List<PageTextLayerDto>> GeneratePageTextLayerAsync(int pageId, string targetLanguage = "Vietnamese")
        {
            // 1. Get the page from DB
            var page = await _context.ChapterPages
                .Include(p => p.Chapter).ThenInclude(c => c.Series)
                .FirstOrDefaultAsync(p => p.PageId == pageId);

            if (page == null)
            {
                throw new Exception("Chapter Page not found: " + pageId);
            }

            // 2. Clear existing layers for this specific config only (not all languages)
            var existingLayers = await _context.PageTextLayers
                .Where(l => l.PageId == pageId
                         && l.SourceLanguage == "auto"
                         && l.TargetLanguage == targetLanguage
                         && l.TranslationProvider == "Gemini")
                .ToListAsync();
            if (existingLayers.Any())
            {
                _context.PageTextLayers.RemoveRange(existingLayers);
                await _context.SaveChangesAsync();
            }

            // 3. Download the image
            byte[] imageBytes;
            try
            {
                imageBytes = await _httpClient.GetByteArrayAsync(page.ImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download image for page ID {PageId} at URL: {Url}", pageId, page.ImageUrl);
                throw new Exception("Failed to download image for OCR.", ex);
            }

            // 4. Extract Text Regions via OCR (Tesseract)
            var regions = await _ocrService.ExtractTextRegionsFromImageAsync(imageBytes);

            if (regions == null || regions.Count == 0)
            {
                return new List<PageTextLayerDto>();
            }

            // 5. Build context for AI translation
            string context = $"Series: {page.Chapter.Series?.Title}. Chapter: {page.Chapter.ChapterNumber}. Page: {page.PageNumber}.";
            
            // Extract texts from regions
            var originalTexts = regions.Select(r => r.Text).ToList();

            // 6. Call AI Translation
            var translatedTexts = await _aiClient.TranslateTextsAsync(originalTexts, targetLanguage, context);

            // 7. Save to DB
            var layersToSave = new List<PageTextLayer>();
            for (int i = 0; i < regions.Count; i++)
            {
                var layer = new PageTextLayer
                {
                    PageId = pageId,
                    X = regions[i].X,
                    Y = regions[i].Y,
                    Width = regions[i].Width,
                    Height = regions[i].Height,
                    OriginalText = originalTexts[i],
                    TranslatedText = i < translatedTexts.Count ? translatedTexts[i] : originalTexts[i],
                    CreatedAt = DateTime.UtcNow,
                    IsVerified = false,
                    SourceLanguage = "auto",
                    TargetLanguage = targetLanguage,
                    TranslationProvider = "Gemini"  // Matches DI: IAiTranslationClient = GeminiTranslationClient
                };
                layersToSave.Add(layer);
            }

            _context.PageTextLayers.AddRange(layersToSave);
            await _context.SaveChangesAsync();

            return layersToSave.Select(l => new PageTextLayerDto
            {
                LayerId = l.LayerId,
                PageId = l.PageId,
                X = l.X,
                Y = l.Y,
                Width = l.Width,
                Height = l.Height,
                OriginalText = l.OriginalText,
                TranslatedText = l.TranslatedText,
                IsVerified = l.IsVerified,
                SourceLanguage = l.SourceLanguage,
                TargetLanguage = l.TargetLanguage,
                TranslationProvider = l.TranslationProvider
            }).ToList();
        }
    }
}
