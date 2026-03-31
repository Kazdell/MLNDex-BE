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

        // Supported translation providers
        private static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
        {
            "Google", "Gemini"
        };

        // =====================================================================
        // PHASE 1: SCAN BOXES ONLY (no translation)
        // Learning Cache: returns user-adjusted boxes if they exist
        // =====================================================================
        public async Task<List<BoxScanResponse>> ScanBoxesAsync(BoxScanRequest request)
        {
            _logger.LogInformation("ScanBoxes: PageId={PageId}, Lang={Lang}", request.PageId, request.SourceLanguage);

            // ── LEARNING CACHE CHECK ──
            // Priority: user-adjusted boxes > auto-detected boxes > fresh scan
            // Group by (TargetLanguage, Provider) to avoid mixing boxes from different configs
            var allCachedLayers = await _context.PageTextLayers
                .AsNoTracking()
                .Where(l => l.PageId == request.PageId
                          && l.SourceLanguage == request.SourceLanguage)
                .OrderByDescending(l => l.IsUserAdjusted)
                .ThenByDescending(l => l.AdjustmentCount)
                .ThenByDescending(l => l.CreatedAt)
                .ToListAsync();

            if (allCachedLayers.Any())
            {
                // Pick a single consistent snapshot: prefer user-adjusted group, then latest
                var bestGroup = allCachedLayers
                    .GroupBy(l => new { l.TargetLanguage, l.TranslationProvider })
                    .OrderByDescending(g => g.Any(l => l.IsUserAdjusted))
                    .ThenByDescending(g => g.Max(l => l.AdjustmentCount))
                    .ThenByDescending(g => g.Max(l => l.CreatedAt))
                    .First();

                _logger.LogInformation("ScanBoxes CACHE HIT: PageId={PageId}, {Count} boxes (Group={Target}/{Provider}, UserAdjusted={Adjusted})",
                    request.PageId, bestGroup.Count(), bestGroup.Key.TargetLanguage, bestGroup.Key.TranslationProvider,
                    bestGroup.Count(l => l.IsUserAdjusted));

                return bestGroup.Select(l => new BoxScanResponse
                {
                    LayerId = l.LayerId,
                    X = l.X, Y = l.Y, Width = l.Width, Height = l.Height,
                    DetectedText = l.OriginalText,
                    IsUserAdjusted = l.IsUserAdjusted
                }).ToList();
            }

            // ── FRESH SCAN ──
            var imageBytes = await DownloadPageImageAsync(request.PageId);
            var regions = await _ocrService.ExtractTextRegionsFromImageAsync(imageBytes, request.SourceLanguage);

            if (regions == null || regions.Count == 0)
                return new List<BoxScanResponse>();

            _logger.LogInformation("ScanBoxes FRESH: PageId={PageId}, detected {Count} boxes", request.PageId, regions.Count);

            return regions
                .Where(r => !string.IsNullOrWhiteSpace(r.Text))
                .Select(r => new BoxScanResponse
                {
                    LayerId = null,  // Not yet saved
                    X = r.X, Y = r.Y, Width = r.Width, Height = r.Height,
                    DetectedText = r.Text,
                    IsUserAdjusted = false
                }).ToList();
        }

        // =====================================================================
        // PHASE 2: TRANSLATE WITH USER-ADJUSTED BOXES
        // Crops image per box → OCR each crop → batch translate → save with metadata
        // =====================================================================
        public async Task<List<OverlayTranslationResponse>> TranslateAdjustedBoxesAsync(BoxTranslateRequest request, int? userId = null)
        {
            // Normalize provider to canonical casing to avoid duplicate cache groups
            string provider = NormalizeProvider(request.Provider);
            _logger.LogInformation("TranslateAdjustedBoxes: PageId={PageId}, {Count} boxes, {Src}→{Tgt} ({Provider})",
                request.PageId, request.Boxes?.Count ?? 0, request.SourceLanguage, request.TargetLanguage, provider);

            if (request.Boxes == null || request.Boxes.Count == 0)
                return new List<OverlayTranslationResponse>();

            // ── DOWNLOAD IMAGE ──
            var imageBytes = await DownloadPageImageAsync(request.PageId);

            // ── CROP + OCR EACH BOX ──
            var ocrResults = new List<(AdjustedBox Box, string Text)>();

            foreach (var box in request.Boxes)
            {
                string text = await _ocrService.ExtractTextFromCroppedRegionAsync(
                    imageBytes, box.X, box.Y, box.Width, box.Height, request.SourceLanguage);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    ocrResults.Add((box, text.Trim()));
                }
            }

            if (ocrResults.Count == 0)
                return new List<OverlayTranslationResponse>();

            // ── BATCH TRANSLATE (route by provider) ──
            var originalTexts = ocrResults.Select(r => r.Text).ToList();
            if (!SupportedProviders.Contains(provider))
                throw new NotSupportedException($"Translation provider '{provider}' is not supported. Supported: {string.Join(", ", SupportedProviders)}");

            List<string> translatedTexts = provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase)
                ? await _aiClient.TranslateTextsAsync(originalTexts, request.TargetLanguage, $"Manga translation from {request.SourceLanguage}")
                : await _googleClient.TranslateTextsAsync(originalTexts, request.SourceLanguage, request.TargetLanguage);

            // ── DELETE OLD LAYERS FOR THIS CONFIG ──
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
            }

            // ── SAVE TO DB WITH LEARNING CACHE METADATA ──
            var layersToSave = new List<Domain.Entities.PageTextLayer>();
            for (int i = 0; i < ocrResults.Count; i++)
            {
                var (box, text) = ocrResults[i];
                layersToSave.Add(new Domain.Entities.PageTextLayer
                {
                    PageId = request.PageId,
                    X = box.X, Y = box.Y,
                    Width = box.Width, Height = box.Height,
                    OriginalText = text,
                    TranslatedText = i < translatedTexts.Count ? translatedTexts[i] : text,
                    CreatedAt = DateTime.UtcNow,
                    IsVerified = false,
                    SourceLanguage = request.SourceLanguage,
                    TargetLanguage = request.TargetLanguage,
                    TranslationProvider = provider,
                    // Learning Cache fields
                    IsUserAdjusted = true,   // User explicitly adjusted these boxes
                    AdjustedByUserId = userId,
                    AdjustmentCount = 1
                });
            }

            _context.PageTextLayers.AddRange(layersToSave);
            await _context.SaveChangesAsync();

            _logger.LogInformation("TranslateAdjustedBoxes: Saved {Count} layers (UserAdjusted) for PageId={PageId}",
                layersToSave.Count, request.PageId);

            return layersToSave.Select(l => new OverlayTranslationResponse
            {
                LayerId = l.LayerId,
                X = l.X, Y = l.Y, Width = l.Width, Height = l.Height,
                OriginalText = l.OriginalText,
                TranslatedText = l.TranslatedText,
                IsUserAdjusted = l.IsUserAdjusted,
                Provider = l.TranslationProvider
            }).ToList();
        }

        // =====================================================================
        // ORIGINAL: One-shot OCR + translate (backward compatible, "Dịch nhanh")
        // =====================================================================
        public async Task<List<OverlayTranslationResponse>> GenerateOverlayTranslationsAsync(OverlayTranslationRequest request)
        {
            string provider = "Google"; // Default auto Google

            // ── STEP 1: DELETE OLD LAYERS ──
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

            // ── STEP 2: DOWNLOAD IMAGE ──
            var imageBytes = await DownloadPageImageAsync(request.PageId);

            // ── STEP 3: OCR EXTRACT ──
            _logger.LogInformation("Using OCR: {Provider} ({Type}) for PageId {PageId}",
                _ocrService.ProviderName, _ocrService.GetType().Name, request.PageId);

            List<Application.Models.OCR.OCRRegion> regions;
            try
            {
                regions = await _ocrService.ExtractTextRegionsFromImageAsync(imageBytes, request.SourceLanguage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OCR failed for PageId {PageId}", request.PageId);
                throw new Exception($"OCR Engine gặp lỗi: {ex.Message}");
            }

            if (regions == null || regions.Count == 0)
                return new List<OverlayTranslationResponse>();

            var validRegions = regions.Where(r => !string.IsNullOrWhiteSpace(r.Text)).ToList();
            if (validRegions.Count == 0) return new List<OverlayTranslationResponse>();

            var originalTexts = validRegions.Select(r => r.Text).ToList();

            // ── STEP 4: BATCH TRANSLATE ──
            List<string> translatedTexts = await _googleClient.TranslateTextsAsync(originalTexts, request.SourceLanguage, request.TargetLanguage);
            
            // ── STEP 5: SAVE TO DB ──
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
                    TranslationProvider = provider,
                    IsUserAdjusted = false,
                    AdjustedByUserId = null,
                    AdjustmentCount = 0
                });
            }

            _context.PageTextLayers.AddRange(layersToSave);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Saved {Count} layers: PageId={PageId} ({Src}→{Tgt}, {Provider})",
                layersToSave.Count, request.PageId, request.SourceLanguage, request.TargetLanguage, provider);

            return layersToSave.Select(l => new OverlayTranslationResponse
            {
                LayerId = l.LayerId,
                X = l.X, Y = l.Y,
                Width = l.Width, Height = l.Height,
                OriginalText = l.OriginalText,
                TranslatedText = l.TranslatedText,
                IsUserAdjusted = l.IsUserAdjusted,
                Provider = l.TranslationProvider
            }).ToList();
        }

        // =====================================================================
        // VISION: Gemini Vision translation for whole page
        // =====================================================================
        public async Task<List<OverlayTranslationResponse>> TranslatePageByAiVisionAsync(int pageId, string sourceLang, string targetLang)
        {
            const string provider = "Gemini_Vision";
            
            // ── STEP 1: DELETE OLD LAYERS ──
            var existingLayers = await _context.PageTextLayers
                .Where(l => l.PageId == pageId
                          && l.SourceLanguage == sourceLang
                          && l.TargetLanguage == targetLang
                          && l.TranslationProvider == provider)
                .ToListAsync();

            if (existingLayers.Any())
            {
                _context.PageTextLayers.RemoveRange(existingLayers);
                await _context.SaveChangesAsync();
            }

            // ── STEP 2: DOWNLOAD & ENCODE IMAGE ──
            var imageBytes = await DownloadPageImageAsync(pageId);
            var base64Image = Convert.ToBase64String(imageBytes);

            // ── STEP 3: CALL VISION API ──
            _logger.LogInformation("Calling Vision AI for PageId {PageId}", pageId);
            var visionResults = await _aiClient.TranslatePageByAiVisionAsync(base64Image, sourceLang, targetLang);

            if (visionResults == null || visionResults.Count == 0)
                return new List<OverlayTranslationResponse>();

            // ── STEP 4: SAVE TO DB ──
            var layersToSave = new List<Domain.Entities.PageTextLayer>();
            foreach (var r in visionResults)
            {
                layersToSave.Add(new Domain.Entities.PageTextLayer
                {
                    PageId = pageId,
                    X = Math.Round(r.X, 2),
                    Y = Math.Round(r.Y, 2),
                    Width = Math.Round(r.Width, 2),
                    Height = Math.Round(r.Height, 2),
                    OriginalText = r.OriginalText,
                    TranslatedText = r.TranslatedText,
                    CreatedAt = DateTime.UtcNow,
                    IsVerified = false,
                    SourceLanguage = sourceLang,
                    TargetLanguage = targetLang,
                    TranslationProvider = provider,
                    IsUserAdjusted = false,
                    AdjustedByUserId = null,
                    AdjustmentCount = 0
                });
            }

            _context.PageTextLayers.AddRange(layersToSave);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vision AI saved {Count} layers for PageId={PageId}", layersToSave.Count, pageId);

            return layersToSave.Select(l => new OverlayTranslationResponse
            {
                LayerId = l.LayerId,
                X = l.X, Y = l.Y,
                Width = l.Width, Height = l.Height,
                OriginalText = l.OriginalText,
                TranslatedText = l.TranslatedText,
                IsUserAdjusted = l.IsUserAdjusted,
                Provider = l.TranslationProvider
            }).ToList();
        }

        // =====================================================================
        // HELPER: Normalize provider string to canonical casing
        // =====================================================================
        private static string NormalizeProvider(string? raw)
        {
            var trimmed = raw?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return "Google";
            if (trimmed.Equals("Google", StringComparison.OrdinalIgnoreCase)) return "Google";
            if (trimmed.Equals("Gemini", StringComparison.OrdinalIgnoreCase)) return "Gemini";
            return trimmed; // unknown values pass through and will be rejected by SupportedProviders check
        }

        // =====================================================================
        // HELPER: Download page image bytes
        // =====================================================================
        private async Task<byte[]> DownloadPageImageAsync(int pageId)
        {
            var page = await _context.ChapterPages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.PageId == pageId);

            if (page == null)
                throw new Exception($"Chapter Page not found: {pageId}");

            try
            {
                return await _httpClient.GetByteArrayAsync(page.ImageUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download image for PageId {PageId} at URL {Url}", pageId, page.ImageUrl);
                throw new Exception("Lỗi khi tải ảnh từ server.");
            }
        }
    }
}
