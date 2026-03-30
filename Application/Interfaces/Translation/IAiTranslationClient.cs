using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces.Translation
{
  public interface IAiTranslationClient
  {
    /// <summary>
    /// Translates a batch of text segments contextually.
    /// </summary>
    /// <param name="texts">List of original text segments (e.g. from OCR)</param>
    /// <param name="targetLanguage">Target language (default Vietnamese)</param>
    /// <param name="context">Optional context (e.g. comic genre, series description, previous pages)</param>
    /// <returns>A list of translated texts, in the exact same order as input</returns>
    Task<List<string>> TranslateTextsAsync(List<string> texts, string targetLanguage = "Vietnamese", string context = "");

    /// <summary>
    /// Translates an entire manga page using Vision AI.
    /// Returns a list of JSON objects containing text, translatedText, x, y, width, height (percentage).
    /// </summary>
    Task<List<Application.DTOs.Translation.OverlayTranslationResponse>> TranslatePageByAiVisionAsync(string base64Image, string sourceLanguage, string targetLanguage);
  }
}
