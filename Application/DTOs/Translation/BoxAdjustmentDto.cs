using System.Collections.Generic;

namespace Application.DTOs.Translation
{
    /// <summary>
    /// Phase 1: Scan boxes only — no translation. Returns raw OCR bounding boxes.
    /// </summary>
    public class BoxScanRequest
    {
        public int PageId { get; set; }
        public string SourceLanguage { get; set; } = "auto";
    }

    public class BoxScanResponse
    {
        public int? LayerId { get; set; }         // null if fresh scan (not yet saved)
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string DetectedText { get; set; } = string.Empty;  // Raw OCR text (untranslated)
        public bool IsUserAdjusted { get; set; }  // true = previously adjusted by a user
    }

    /// <summary>
    /// Phase 2: Translate with user-adjusted boxes. FE sends adjusted box positions.
    /// </summary>
    public class BoxTranslateRequest
    {
        public int PageId { get; set; }
        public string SourceLanguage { get; set; } = "auto";
        public string TargetLanguage { get; set; } = "vi";
        public string Provider { get; set; } = "Google";
        public List<AdjustedBox> Boxes { get; set; } = new();
    }

    public class AdjustedBox
    {
        public int? LayerId { get; set; }   // null = new box added by user
        public double X { get; set; }       // % of image width
        public double Y { get; set; }       // % of image height
        public double Width { get; set; }   // % of image width
        public double Height { get; set; }  // % of image height
    }
}
