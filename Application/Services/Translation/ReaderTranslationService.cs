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
        private readonly IEnumerable<IOCRService> _ocrServices;
        private readonly IAiTranslationClient _aiClient;
        private readonly IGoogleTranslationClient _googleClient;
        private readonly ILogger<ReaderTranslationService> _logger;
        private readonly HttpClient _httpClient;

        public ReaderTranslationService(
            IMlndexDbContext context,
            IEnumerable<IOCRService> ocrServices,
            IAiTranslationClient aiClient,
            IGoogleTranslationClient googleClient,
            ILogger<ReaderTranslationService> logger,
            HttpClient httpClient)
        {
            _context = context;
            _ocrServices = ocrServices;
            _aiClient = aiClient;
            _googleClient = googleClient;
            _logger = logger;
            _httpClient = httpClient;
        }

        private IOCRService GetOcrService(string ocrProvider)
        {
            // Route to specific OCR engine based on provider string
            // Uses ProviderName from IOCRService interface (no concrete type dependency)
            var matched = _ocrServices.FirstOrDefault(s => 
                s.ProviderName.Equals(ocrProvider, StringComparison.OrdinalIgnoreCase));
            
            return matched ?? _ocrServices.First();
        }

        public async Task<List<OverlayTranslationResponse>> GenerateOverlayTranslationsAsync(OverlayTranslationRequest request)
        {
            string provider = request.Provider ?? "Google";

            // ──────────────────────────────────────────────────────────────
            // [CACHE-READY] Uncomment when OCR quality is stable.
            // If DB already has layers for (PageId+SrcLang+TgtLang+Provider)
            // → return immediately, skip entire OCR + Translation pipeline.
            // ──────────────────────────────────────────────────────────────
            // var cachedLayers = await _context.PageTextLayers
            //     .AsNoTracking()
            //     .Where(l => l.PageId == request.PageId
            //               && l.SourceLanguage == request.SourceLanguage
            //               && l.TargetLanguage == request.TargetLanguage
            //               && l.TranslationProvider == provider)
            //     .ToListAsync();
            //
            // if (cachedLayers.Any())
            // {
            //     _logger.LogInformation("Cache HIT PageId={PageId}, {Src}→{Tgt} ({Provider}, {Count} layers)",
            //         request.PageId, request.SourceLanguage, request.TargetLanguage, provider, cachedLayers.Count);
            //     return cachedLayers.Select(l => new OverlayTranslationResponse
            //     {
            //         X = l.X, Y = l.Y, Width = l.Width, Height = l.Height,
            //         OriginalText = l.OriginalText, TranslatedText = l.TranslatedText
            //     }).ToList();
            // }

            // ===== STEP 1: DELETE OLD LAYERS (Rebuild each time — test phase) =====
            var existingLayers = await _context.PageTextLayers
                .Where(l => l.PageId == request.PageId
                          && l.SourceLanguage == request.SourceLanguage
                          && l.TargetLanguage == request.TargetLanguage
                          && l.TranslationProvider == provider)
                .ToListAsync();

            if (existingLayers.Any())
            {
                _context.PageTextLayers.RemoveRange(existingLayers);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Cleared {Count} old layers for PageId={PageId} ({Src}→{Tgt}, {Provider})",
                    existingLayers.Count, request.PageId, request.SourceLanguage, request.TargetLanguage, provider);
            }

            // ===== STEP 2: FETCH PAGE =====
            var page = await _context.ChapterPages
                .AsNoTracking()
                .Include(p => p.Chapter).ThenInclude(c => c.Series)
                .FirstOrDefaultAsync(p => p.PageId == request.PageId);

            if (page == null)
                throw new Exception($"Chapter Page not found: {request.PageId}");

            // ===== STEP 3: DOWNLOAD IMAGE =====
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

            // ===== STEP 4: OCR EXTRACT =====
            var ocrService = GetOcrService(request.OcrProvider);
            _logger.LogInformation("Using OCR: {Provider} ({Type}) for PageId {PageId}",
                request.OcrProvider, ocrService.GetType().Name, request.PageId);

            List<Application.Models.OCR.OCRRegion> regions;
            try
            {
                regions = await ocrService.ExtractTextRegionsFromImageAsync(imageBytes, request.SourceLanguage);
            }
            catch (Exception ex)
            {
                // Tesseract error → hard error (no fallback, notify user)
                if (request.OcrProvider.Equals("tesseract", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogError(ex, "Tesseract OCR failed for PageId {PageId}", request.PageId);
                    throw new Exception($"OCR Engine (Tesseract) gặp lỗi: {ex.Message}");
                }

                // ──────────────────────────────────────────────────────────────
                // [FALLBACK-READY] Uncomment when ONNX/Paddle quality is stable.
                // ONNX error → auto fallback to Tesseract.
                // ──────────────────────────────────────────────────────────────
                // _logger.LogWarning(ex, "OCR {Provider} failed for PageId {PageId}. Falling back to Tesseract.",
                //     request.OcrProvider, request.PageId);
                // var fallbackOcr = GetOcrService("tesseract");
                // regions = await fallbackOcr.ExtractTextRegionsFromImageAsync(imageBytes, request.SourceLanguage);

                // [CURRENT] ONNX error → hard error (isolate each engine during testing)
                _logger.LogError(ex, "OCR {Provider} failed for PageId {PageId}. Fallback disabled.",
                    request.OcrProvider, request.PageId);
                throw new Exception($"OCR Engine ({request.OcrProvider}) gặp lỗi: {ex.Message}");
            }

            if (regions == null || regions.Count == 0)
                return new List<OverlayTranslationResponse>();

            var validRegions = regions.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
            if (validRegions.Count == 0) return new List<OverlayTranslationResponse>();

            var originalTexts = validRegions.Select(r => r.Text).ToList();

            // ===== STEP 5: BATCH TRANSLATE =====
            List<string> translatedTexts;

            if (provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            {
                string contextStr = $"Translate to {request.TargetLanguage}. Source: {request.SourceLanguage}. Series: {page.Chapter?.Series?.Title}.";
                translatedTexts = await _aiClient.TranslateTextsAsync(originalTexts, request.TargetLanguage, contextStr);
            }
            else
            {
                translatedTexts = await _googleClient.TranslateTextsAsync(originalTexts, request.SourceLanguage, request.TargetLanguage);
            }

            // ===== STEP 6: SAVE TO DB WITH METADATA =====
            var layersToSave = new List<Domain.Entities.PageTextLayer>();
            for (int i = 0; i < validRegions.Count; i++)
            {
                var r = validRegions[i];
                layersToSave.Add(new Domain.Entities.PageTextLayer
                {
                    PageId = request.PageId,
                    X = r.X, Y = r.Y,
                    Width = r.Width, Height = r.Height,
                    OriginalText = r.Text,
                    TranslatedText = i < translatedTexts.Count ? translatedTexts[i] : r.Text,
                    CreatedAt = DateTime.UtcNow,
                    IsVerified = false,
                    SourceLanguage = request.SourceLanguage,
                    TargetLanguage = request.TargetLanguage,
                    TranslationProvider = provider
                });
            }

            _context.PageTextLayers.AddRange(layersToSave);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Saved {Count} layers: PageId={PageId} ({Src}→{Tgt}, {Provider})",
                layersToSave.Count, request.PageId, request.SourceLanguage, request.TargetLanguage, provider);

            // ===== STEP 7: RESPONSE =====
            return layersToSave.Select(l => new OverlayTranslationResponse
            {
                X = l.X, Y = l.Y,
                Width = l.Width, Height = l.Height,
                OriginalText = l.OriginalText,
                TranslatedText = l.TranslatedText
            }).ToList();
        }
    }
}
