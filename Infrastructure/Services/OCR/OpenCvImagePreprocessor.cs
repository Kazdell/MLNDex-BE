using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Application.Interfaces.OCR;
using OpenCvSharp;
using Application.Models.OCR;
using Microsoft.Extensions.Options;

namespace Infrastructure.Services.OCR
{
  public class OpenCvImagePreprocessor : IImagePreprocessorService
  {
    private readonly OcrSettings _settings;

    public OpenCvImagePreprocessor(IOptionsMonitor<OcrSettings> options)
    {
      _settings = options.CurrentValue;
    }

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

        // Bước 2: Binarization (Phân mảnh Trắng / Đen)
        using var threshMat = new Mat();

        if ("Adaptive".Equals(_settings.BinarizationMode, System.StringComparison.OrdinalIgnoreCase))
        {
            // [Chế độ cũ]: Gây vỡ rỗng nét chữ to
            Cv2.AdaptiveThreshold(grayMat, threshMat, 255,
                                  AdaptiveThresholdTypes.GaussianC,
                                  ThresholdTypes.Binary, 11, 2);
        }
        else
        {
            // [Chế độ mới]: Chuẩn Nhật Manga / Giữ nguyên nét to đậm
            Cv2.Threshold(grayMat, threshMat, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            // Thuật toán gỡ rối chữ trắng nền đen (Invert)
            // Otsu tạo ra White(255) và Black(0). Box manga thường có nền là trắng (255) và Mực chữ là đen (0).
            // Nếu Box có số lượng điểm nền Đen (Dark Background) chiếm > 50%, thì Tesseract sẽ mù.
            // Giải pháp: Invert nó thành chữ Đen nền Trắng.
            int totalPixels = threshMat.Width * threshMat.Height;
            int whitePixels = Cv2.CountNonZero(threshMat);
            int blackPixels = totalPixels - whitePixels;

            if (blackPixels > totalPixels * 0.5)
            {
                Cv2.BitwiseNot(threshMat, threshMat);
            }
        }

        // Đóng gói trả về Stream PNG (sẵn sàng ném vào Tesseract)
        var resultBytes = threshMat.ToBytes(".png");
        results.Add(new MemoryStream(resultBytes));
      }

      return results;
    }
  }
}
