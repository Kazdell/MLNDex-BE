using System;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

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

        [HttpPost("overlay")]
        // Remove Authorize attribute to allow guests/readers. Or keep if auth required.
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
    }
}
