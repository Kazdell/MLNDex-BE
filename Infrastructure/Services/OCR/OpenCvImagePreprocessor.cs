using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Application.Interfaces.OCR;
using OpenCvSharp;

namespace Infrastructure.Services.OCR
{
  public class OpenCvImagePreprocessor : IImagePreprocessorService
  {
    public async Task<List<Stream>> CutAndCleanBoxesAsync(Stream originalImageStream, List<BoundingBoxDto> boxes)
    {
      // Đảm bảo Stream đang ở vạch xuất phát
      if (originalImageStream.CanSeek)
        originalImageStream.Position = 0;

      byte[] imageBytes;
      using (var memoryStream = new MemoryStream())
      {
        await originalImageStream.CopyToAsync(memoryStream);
        imageBytes = memoryStream.ToArray();
      }

      // Giải mã ảnh trên RAM (Tốc độ cao)
      using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
      var results = new List<Stream>();

      foreach (var box in boxes)
      {
        int x = System.Math.Max(0, box.X);
        int y = System.Math.Max(0, box.Y);
        int w = System.Math.Min(box.Width, mat.Width - x);
        int h = System.Math.Min(box.Height, mat.Height - y);

        // Nếu box không hợp lệ, bỏ qua
        if (w <= 0 || h <= 0) continue;

        var rect = new Rect(x, y, w, h);
        using var croppedMat = new Mat(mat, rect);

        // Bước 1: Grayscale (Loại bỏ nhiễu màu sắc của manga/webtoon)
        using var grayMat = new Mat();
        Cv2.CvtColor(croppedMat, grayMat, ColorConversionCodes.BGR2GRAY);

        // Bước 2: Adaptive Thresholding (Kỹ thuật đỉnh cao giúp biến nền xám u ám thành Trắng tinh, chữ đen đậm nét)
        // Size khối là 11, hằng số là 2 (Chuẩn cho chữ Manga nhỏ)
        using var threshMat = new Mat();
        Cv2.AdaptiveThreshold(grayMat, threshMat, 255,
                              AdaptiveThresholdTypes.GaussianC,
                              ThresholdTypes.Binary, 11, 2);

        // Đóng gói trả về Stream PNG (sẵn sàng ném vào Tesseract)
        var resultBytes = threshMat.ToBytes(".png");
        results.Add(new MemoryStream(resultBytes));
      }

      return results;
    }
  }
}
