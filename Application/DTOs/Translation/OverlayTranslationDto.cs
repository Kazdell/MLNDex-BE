namespace Application.DTOs.Translation
{
    public class OverlayTranslationRequest
    {
        public int PageId { get; set; }
        public string SourceLanguage { get; set; } = "auto"; // ISO 639-1: auto, zh, ja, ko, en, vi
        public string TargetLanguage { get; set; } = "vi";   // ISO 639-1: vi, en, ja, ko, zh
        public string Provider { get; set; } = "Google";      // Translation provider: "Google" or "OpenAI"
        public string OcrProvider { get; set; } = "onnx";     // OCR engine: "onnx" or "tesseract"
    }

    public class OverlayTranslationResponse
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
    }
}
