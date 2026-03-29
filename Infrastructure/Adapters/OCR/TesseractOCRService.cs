using Application.Interfaces.Moderation;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;
using SixLabors.ImageSharp;

namespace Infrastructure.Adapters.OCR
{
  public class TesseractOCRService : IOCRService
  {
    private readonly string _dataPath; // Đường dẫn đến thư mục tessdata
    private readonly ILogger<TesseractOCRService> _logger;

    public TesseractOCRService(ILogger<TesseractOCRService> logger)
    {
      _logger = logger;
      // Lấy đường dẫn thư mục thực thi để tìm thư mục tessdata đi kèm project
      _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
    }

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes)
    {
      return await Task.Run(() =>
      {
        try
        {
          // Chuyển đổi WebP (hoặc các định dạng khác) sang PNG cho Tesseract dễ đọc vì Tesseract/Leptonica build sẵn thường thiếu webp
          byte[] pngBytes;
          using (var memStream = new MemoryStream(imageBytes))
          {
            using var image = Image.Load(memStream);
            using var outStream = new MemoryStream();
            image.SaveAsPng(outStream);
            pngBytes = outStream.ToArray();
          }

          using var engine = new TesseractEngine(_dataPath, "vie+eng", EngineMode.Default);
          using var img = Pix.LoadFromMemory(pngBytes);
          using var page = engine.Process(img);

          var text = page.GetText();
          return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Lỗi khi xử lý OCR với Tesseract.");
          throw;
        }
      });
    }

    public async Task<List<Application.Models.OCR.OCRRegion>> ExtractTextRegionsFromImageAsync(byte[] imageBytes)
    {
      return await Task.Run(() =>
      {
        var regions = new List<Application.Models.OCR.OCRRegion>();
        try
        {
          byte[] pngBytes;
          using (var memStream = new MemoryStream(imageBytes))
          {
            using var image = Image.Load(memStream);
            using var outStream = new MemoryStream();
            image.SaveAsPng(outStream);
            pngBytes = outStream.ToArray();
          }

          using var engine = new TesseractEngine(_dataPath, "vie+eng", EngineMode.Default);
          using var img = Pix.LoadFromMemory(pngBytes);
          using var page = engine.Process(img);

          using var iter = page.GetIterator();
          iter.Begin();
          do
          {
            if (iter.TryGetBoundingBox(PageIteratorLevel.Block, out var rect))
            {
              var text = iter.GetText(PageIteratorLevel.Block);
              if (!string.IsNullOrWhiteSpace(text))
              {
                regions.Add(new Application.Models.OCR.OCRRegion
                {
                  Text = text.Trim(),
                  X = rect.X1,
                  Y = rect.Y1,
                  Width = rect.Width,
                  Height = rect.Height
                });
              }
            }
          } while (iter.Next(PageIteratorLevel.Block));

          return regions;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Lỗi khi trích xuất vùng văn bản với Tesseract OCR.");
          throw;
        }
      });
    }
  }
}
