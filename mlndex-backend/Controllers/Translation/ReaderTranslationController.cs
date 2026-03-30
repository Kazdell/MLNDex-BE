using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace mlndex_backend.Controllers.Translation
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReaderTranslationController : ControllerBase
    {
        private readonly IReaderTranslationService _translationService;
        private readonly ILogger<ReaderTranslationController> _logger;

        public ReaderTranslationController(
            IReaderTranslationService translationService,
            ILogger<ReaderTranslationController> logger)
        {
            _translationService = translationService;
            _logger = logger;
        }

        /// <summary>
        /// Original one-shot OCR + translate (backward compatible, "Dịch nhanh")
        /// </summary>
        [HttpPost("overlay")]
        public async Task<IActionResult> GetOverlayTranslations([FromBody] OverlayTranslationRequest request)
        {
            try
            {
                if (request == null || request.PageId <= 0)
                {
                    return BadRequest(new ApiResponse<object>(false, "Invalid payload.", null, "400"));
                }

                var overlays = await _translationService.GenerateOverlayTranslationsAsync(request);
                return Ok(new ApiResponse<object>(true, "Dịch thành công", overlays));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý dịch overlay cho trang {PageId}", request?.PageId);
                return StatusCode(500, new ApiResponse<object>(false, ex.Message, null, "500"));
            }
        }

        /// <summary>
        /// Phase 1: Scan boxes only — returns detected bounding boxes without translation.
        /// Uses Learning Cache: returns user-adjusted boxes if they exist.
        /// </summary>
        [HttpPost("scan-boxes")]
        public async Task<IActionResult> ScanBoxes([FromBody] BoxScanRequest request)
        {
            try
            {
                if (request == null || request.PageId <= 0)
                {
                    return BadRequest(new ApiResponse<object>(false, "Invalid payload.", null, "400"));
                }

                var boxes = await _translationService.ScanBoxesAsync(request);
                return Ok(new ApiResponse<object>(true, "Quét thành công", boxes));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi quét boxes cho trang {PageId}", request?.PageId);
                return StatusCode(500, new ApiResponse<object>(false, ex.Message, null, "500"));
            }
        }

        /// <summary>
        /// Phase 2: Translate with user-adjusted boxes.
        /// Crops image per box, OCR each crop, then batch translate.
        /// </summary>
        [HttpPost("translate-boxes")]
        public async Task<IActionResult> TranslateAdjustedBoxes([FromBody] BoxTranslateRequest request)
        {
            try
            {
                if (request == null || request.PageId <= 0)
                {
                    return BadRequest(new ApiResponse<object>(false, "Invalid payload.", null, "400"));
                }

                // Try to get userId if authenticated (for Learning Cache tracking)
                int? userId = null;
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("UserId")?.Value;
                if (int.TryParse(userIdClaim, out var parsedId))
                    userId = parsedId;

                var overlays = await _translationService.TranslateAdjustedBoxesAsync(request, userId);
                return Ok(new ApiResponse<object>(true, "Dịch thành công", overlays));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dịch adjusted boxes cho trang {PageId}", request?.PageId);
                return StatusCode(500, new ApiResponse<object>(false, ex.Message, null, "500"));
            }
        }

        /// <summary>
        /// Phase 3: Translate full page using GPT-4o-mini Vision
        /// </summary>
        [HttpPost("vision-scan-page")]
        public async Task<IActionResult> TranslatePageByAiVision([FromBody] OverlayTranslationRequest request)
        {
            try
            {
                if (request == null || request.PageId <= 0)
                {
                    return BadRequest(new ApiResponse<object>(false, "Invalid payload.", null, "400"));
                }

                var overlays = await _translationService.TranslatePageByAiVisionAsync(request.PageId, request.SourceLanguage, request.TargetLanguage);
                return Ok(new ApiResponse<object>(true, "Dịch Vision thành công", overlays));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi dịch Vision page cho trang {PageId}", request?.PageId);
                return StatusCode(500, new ApiResponse<object>(false, ex.Message, null, "500"));
            }
        }
    }
}
