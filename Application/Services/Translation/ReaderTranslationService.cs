using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Application.Interfaces.Translation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Translation
{
    public class ReaderTranslationService : IReaderTranslationService
    {
        private readonly IMlndexDbContext _context;
        private readonly IOCRService _ocrService;
        private readonly IAiTranslationClient _aiClient;
        private readonly IGoogleTranslationClient _googleClient;
        private readonly ILogger<ReaderTranslationService> _logger;
        private readonly HttpClient _httpClient;

        public ReaderTranslationService(
            IMlndexDbContext context,
            IOCRService ocrService,
            IAiTranslationClient aiClient,
            IGoogleTranslationClient googleClient,
            ILogger<ReaderTranslationService> logger,
            HttpClient httpClient)
        {
            _context = context;
            _ocrService = ocrService;
            _aiClient = aiClient;
            _googleClient = googleClient;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task<List<OverlayTranslationResponse>> GenerateOverlayTranslationsAsync(OverlayTranslationRequest request)
        {
            // 1. Fetch image URL
            var page = await _context.ChapterPages
                .AsNoTracking()
                .Include(p => p.Chapter).ThenInclude(c => c.Series)
                .FirstOrDefaultAsync(p => p.PageId == request.PageId);

            if (page == null)
            {
                throw new Exception($"Chapter Page not found: {request.PageId}");
            }

            // 2. Download Image Bytes
            byte[] imageBytes;
            try
            {
                imageBytes = await _httpClient.GetByteArrayAsync(page.ImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download image for PageId {PageId} at URL {Url}", request.PageId, page.ImageUrl);
                throw new Exception("Lỗi khi tải ảnh từ server.");
            }

            // 3. Extract Texts and Bounding Boxes (OCR)
            var regions = await _ocrService.ExtractTextRegionsFromImageAsync(imageBytes, request.SourceLanguage);

            if (regions == null || regions.Count == 0)
            {
                return new List<OverlayTranslationResponse>();
            }

            // Filter out empty regions to avoid wasting API calls
            var validRegions = regions.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
            if (validRegions.Count == 0) return new List<OverlayTranslationResponse>();

            var originalTexts = validRegions.Select(r => r.Text).ToList();
            List<string> translatedTexts;

            // 4. Translate based on preferred provider
            string provider = request.Provider ?? "Google";
            
            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                string contextStr = $"Dịch sang tiếng Việt cho ảnh truyện tranh. Ngôn ngữ nguồn là {request.SourceLanguage}. Context: Truyện mang tên {page.Chapter?.Series?.Title}.";
                translatedTexts = await _aiClient.TranslateTextsAsync(originalTexts, request.TargetLanguage, contextStr);
            }
            else // Default to Google
            {
                translatedTexts = await _googleClient.TranslateTextsAsync(originalTexts, request.SourceLanguage, request.TargetLanguage);
            }

            // 5. Build Response mapping original bounding boxes back to translated text
            var responseList = new List<OverlayTranslationResponse>();
            for (int i = 0; i < validRegions.Count; i++)
            {
                var r = validRegions[i];
                var translated = i < translatedTexts.Count ? translatedTexts[i] : r.Text;
                
                responseList.Add(new OverlayTranslationResponse
                {
                    X = r.X,
                    Y = r.Y,
                    Width = r.Width,
                    Height = r.Height,
                    OriginalText = r.Text,
                    TranslatedText = translated
                });
            }

            return responseList;
        }
    }
}
