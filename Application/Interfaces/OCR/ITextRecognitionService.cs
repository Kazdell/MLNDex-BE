using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces.OCR
{
  /// <summary>
  /// Chịu trách nhiệm hoàn toàn về việc dịch pixel ảnh sang Text. 
  /// (Tesseract, MangaOcr, GeminiVision,...)
  /// Nằm độc lập với quá trình cắt bounding box của AI Detectors.
  /// </summary>
  public interface ITextRecognitionService
  {
    Task<string> RecognizeTextAsync(byte[] croppedImageBytes, string languageCode = "auto");
  }
}
