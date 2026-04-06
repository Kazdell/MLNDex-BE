using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.User;

namespace Application.Interfaces.Translation
{
  public interface IReaderTranslationService
  {
    // Original: auto OCR + translate in one shot (backward compatible)
    Task<List<OverlayTranslationResponse>> GenerateOverlayTranslationsAsync(OverlayTranslationRequest request);

    // Phase 1: Scan boxes only — returns bounding boxes without translation
    // Uses Learning Cache: if user-adjusted boxes exist, returns those instead of re-scanning
    Task<List<BoxScanResponse>> ScanBoxesAsync(BoxScanRequest request);

    // Phase 2: Translate with user-adjusted boxes — crops image per box, OCR, then translate
    Task<List<OverlayTranslationResponse>> TranslateAdjustedBoxesAsync(BoxTranslateRequest request, int? userId = null);

    // Vision: Translate full page via GPT-4o-mini Vision
    Task<List<OverlayTranslationResponse>> TranslatePageByAiVisionAsync(int pageId, string sourceLang, string targetLang);
  }
}
