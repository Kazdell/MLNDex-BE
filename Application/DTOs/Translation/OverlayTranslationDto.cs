namespace Application.DTOs.Translation
{
    public class OverlayTranslationRequest
    {
        public int PageId { get; set; }
        public string SourceLanguage { get; set; } = "auto"; // ISO 639-1: auto, zh, ja, ko, en, vi
        public string TargetLanguage { get; set; } = "vi";   // ISO 639-1: vi, en, ja, ko, zh
        public string Provider { get; set; } = "Google";      // Translation provider: "Google" or "OpenAI"
    }

    public class OverlayTranslationResponse
    {
        public int LayerId { get; set; }          // Unique ID for tracking individual boxes
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
        public bool IsUserAdjusted { get; set; }  // Was this box manually adjusted?
        public string Provider { get; set; }      // To identify if it's from Vision AI or standard OCR
    }
}
