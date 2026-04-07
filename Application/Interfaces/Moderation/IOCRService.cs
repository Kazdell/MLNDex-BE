using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Moderation
{
  public interface IOCRService
  {
    /// <summary>
    /// Identifier for this OCR provider (e.g., "tesseract", "onnx")
    /// </summary>
    string ProviderName { get; }

    // Nhận vào byte array của ảnh và trả về văn bản nhận diện được
    Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng");

    // Trả về danh sách các vùng văn bản kèm tọa độ
    Task<List<Application.Models.OCR.OCRRegion>> ExtractTextRegionsFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng");

    /// <summary>
    /// Crop image to specific region (% coords) and OCR only that area.
    /// WARNING: Default implementation falls back to full image OCR, ignoring crop params!
    /// New providers MUST override this method for correct behavior.
    /// </summary>
    Task<string> ExtractTextFromCroppedRegionAsync(byte[] imageBytes, double xPct, double yPct, double wPct, double hPct, string languageCode = "auto")
      => ExtractTextFromImageAsync(imageBytes, languageCode);
  }
}
