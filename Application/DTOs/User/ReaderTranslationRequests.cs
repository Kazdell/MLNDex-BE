using System.Collections.Generic;

namespace Application.DTOs.User
{
  // Request overlay translations for a manga page
  public class OverlayTranslationRequest
  {
    public int PageId { get; set; }
    public string SourceLanguage { get; set; } = "auto";
    public string TargetLanguage { get; set; } = "vi";
    public string Provider { get; set; } = "Google";
    public string OcrProvider { get; set; } = "server_tesseract";
  }

  // Phase 1: Scan boxes only — no translation
  public class BoxScanRequest
  {
    public int PageId { get; set; }
    public string SourceLanguage { get; set; } = "auto";
    public string OcrProvider { get; set; } = "server_tesseract";
  }

  // Phase 2: Translate with user-adjusted boxes
  public class BoxTranslateRequest
  {
    public int PageId { get; set; }
    public string SourceLanguage { get; set; } = "auto";
    public string TargetLanguage { get; set; } = "vi";
    public string Provider { get; set; } = "Google"; // Translation Provider
    public string OcrProvider { get; set; } = "server_tesseract"; // Engine Scan
    public bool IsUserAdjusted { get; set; } = true;
    public List<AdjustedBox> Boxes { get; set; } = new();
  }

  // Individual adjusted box coordinates
  public class AdjustedBox
  {
    public int? LayerId { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string OriginalText { get; set; }
  }
}
