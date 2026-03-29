namespace Application.DTOs.Translation
{
    public class OverlayTranslationRequest
    {
        public int PageId { get; set; }
        public string SourceLanguage { get; set; } = "eng"; // jpn, kor, chi_sim, eng
        public string TargetLanguage { get; set; } = "vie";
        public string Provider { get; set; } = "Google"; // "Google" or "OpenAI"
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
