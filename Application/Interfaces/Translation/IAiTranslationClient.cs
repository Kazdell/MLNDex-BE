using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces.Translation
{
  public interface IAiTranslationClient
  {
    /// <summary>
    /// Translates a batch of text segments contextually.
    /// </summary>
    Task<List<string>> TranslateTextsAsync(List<string> texts, string targetLanguage = "Vietnamese", string context = "");

    /// <summary>
    /// Translates an entire manga page using Vision AI.
    /// Returns overlay translation results with bounding boxes.
    /// </summary>
    Task<List<Application.DTOs.User.OverlayTranslationResponse>> TranslatePageByAiVisionAsync(string base64Image, string sourceLanguage, string targetLanguage);
  }
}
