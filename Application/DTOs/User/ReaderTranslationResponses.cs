namespace Application.DTOs.User
{
  // Overlay translation result for a text region
  public class OverlayTranslationResponse
  {
    public int LayerId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public bool IsUserAdjusted { get; set; }
    public string Provider { get; set; } = string.Empty;
  }

  // Phase 1 scan result — raw OCR bounding box
  public class BoxScanResponse
  {
    public int? LayerId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string DetectedText { get; set; } = string.Empty;
    public bool IsUserAdjusted { get; set; }
  }

  // Page text layer data for reader cache
  public class PageTextLayerResponse
  {
    public int LayerId { get; set; }
    public int PageId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string OriginalText { get; set; } = string.Empty;
    public string TranslatedText { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public string SourceLanguage { get; set; } = string.Empty;
    public string TargetLanguage { get; set; } = string.Empty;
    public string TranslationProvider { get; set; } = string.Empty;
  }
}
