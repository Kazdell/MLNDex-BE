using Application.Interfaces.Moderation;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tesseract;
using OpenCvSharp;

namespace Infrastructure.Adapters.OCR
{
  public class TesseractOCRService : IOCRService
  {
    private readonly string _dataPath; // Path to tessdata directory
    private readonly ILogger<TesseractOCRService> _logger;

    public string ProviderName => "tesseract";

    public TesseractOCRService(ILogger<TesseractOCRService> logger)
    {
      _logger = logger;
      // Dữ liệu tessdata giờ phải trỏ vào folder đã tải của tesseract
      _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
    }

    /// <summary>
    /// Map ISO 639-1 standard codes to Tesseract language codes.
    /// Backward compatible: existing Tesseract codes pass through unchanged.
    /// </summary>
    private static string MapToTesseractCode(string isoCode)
    {
      if (string.IsNullOrWhiteSpace(isoCode)) return "eng";

      return isoCode.ToLowerInvariant().Trim() switch
      {
        "auto" => "eng+jpn+chi_sim+kor+vie",  // Multi-lang detection
        "zh"   => "chi_sim",
        "ja"   => "jpn",
        "ko"   => "kor",
        "en"   => "eng",
        "vi"   => "vie",
        // Backward compatibility: pass through if already Tesseract code
        "chi_sim" or "jpn" or "kor" or "eng" or "vie" => isoCode.ToLowerInvariant(),
        _ => isoCode  // Unknown → pass through
      };
    }

    private byte[] PreprocessImageWithOpenCV(byte[] imageBytes, bool invert = false)
    {
        using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (src.Empty()) return imageBytes; // fallback

        /* 
         * THUẬT TOÁN KHỬ NHIỄU OPENCV (ĐỂ DÀNH NẾU CẦN THÊM ĐỘ CHÍNH XÁC CAO NHƯNG TỐN THỜI GIAN)
         * - Cách dùng: Bỏ comment đoạn mã dưới. Nó sẽ tốn thêm ~1s CPU nhưng làm sạch hoàn toàn
         * background manga nhiều sạn.
         * 
         * using var denoised = new Mat();
         * Cv2.FastNlMeansDenoisingColored(src, denoised, 5.5f);
         * var processImg = denoised;
         */
        var processImg = src;

        using var gray = new Mat();
        Cv2.CvtColor(processImg, gray, ColorConversionCodes.BGR2GRAY);

        using var binarized = new Mat();
        // Áp dụng Otsu Thresholding tự động tìm lằn ranh nền trắng chữ đen (chuẩn Manga)
        Cv2.Threshold(gray, binarized, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        if (invert)
        {
            Cv2.BitwiseNot(binarized, binarized);
        }

        Cv2.ImEncode(".png", binarized, out byte[] pngBytes);
        return pngBytes;
    }

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng")
    {
      languageCode = MapToTesseractCode(languageCode);
      return await Task.Run(() =>
      {
        try
        {
          byte[] pngBytes = PreprocessImageWithOpenCV(imageBytes);

          using var engine = new TesseractEngine(_dataPath, languageCode, EngineMode.LstmOnly);
          using var img = Pix.LoadFromMemory(pngBytes);
          using var page = engine.Process(img, PageSegMode.SparseText);

          var text = page.GetText();
          return string.IsNullOrWhiteSpace(text) ? string.Empty : text;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error processing OCR with Tesseract.");
          throw;
        }
      });
    }

    public async Task<List<Application.Models.OCR.OCRRegion>> ExtractTextRegionsFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng")
    {
      languageCode = MapToTesseractCode(languageCode);
      return await Task.Run(() =>
      {
        var regions = new List<Application.Models.OCR.OCRRegion>();
        try
        {
          // Tiền xử lý gốc bằng Otsu
          byte[] pngBytes = PreprocessImageWithOpenCV(imageBytes);

          // Lấy kích thước ảnh gốc thông qua OpenCV Decode siêu nhẹ
          int imageWidth = 0; int imageHeight = 0;
          using (var src = Cv2.ImDecode(imageBytes, ImreadModes.Color)) {
              imageWidth = src.Width;
              imageHeight = src.Height;
          }

          var actualLang = languageCode;
          if (languageCode.Contains("jpn"))
          {
              if (File.Exists(Path.Combine(_dataPath, "jpn_vert.traineddata")))
                 actualLang = "jpn_vert+jpn"; // Ưu tiên chữ dọc (Vertical Japanese)
              else
                 actualLang = "jpn"; 
          }
          else if (languageCode.Contains("kor"))
          {
              if (File.Exists(Path.Combine(_dataPath, "kor_vert.traineddata")))
                 actualLang = "kor_vert+kor";
              else
                 actualLang = "kor";
          }
          else if (languageCode.Contains("chi_sim"))
          {
              if (File.Exists(Path.Combine(_dataPath, "chi_sim_vert.traineddata")))
                 actualLang = "chi_sim_vert+chi_sim";
              else
                 actualLang = "chi_sim";
          }
          else if (languageCode.Contains("chi_tra"))
          {
              if (File.Exists(Path.Combine(_dataPath, "chi_tra_vert.traineddata")))
                 actualLang = "chi_tra_vert+chi_tra";
              else
                 actualLang = "chi_tra";
          }
          
          TesseractEngine engine;
          try 
          {
              engine = new TesseractEngine(_dataPath, actualLang, EngineMode.LstmOnly);
          }
          catch (Exception initEx)
          {
              _logger.LogWarning(initEx, "Failed to initialize Tesseract with {Lang}. Falling back to 'eng'", actualLang);
              engine = new TesseractEngine(_dataPath, "eng", EngineMode.LstmOnly);
          }

          using (engine)
          {
              void ExtractBlocks(Pix imgPix, List<Application.Models.OCR.OCRRegion> targetRegions)
              {
                  // Sử dụng chế độ SparseText để tìm kiếm tốt nhất text rải rác trong manga
                  using var page = engine.Process(imgPix, PageSegMode.SparseText);
                  using var iter = page.GetIterator();
                  iter.Begin();

                  var rawLines = new List<(string Text, int X, int Y, int W, int H)>();
                  do
                  {
                      var level = PageIteratorLevel.TextLine;
                      if (iter.TryGetBoundingBox(level, out var rect))
                      {
                          var text = iter.GetText(level);
                          var confidence = iter.GetConfidence(level);
                          
                          if (!string.IsNullOrWhiteSpace(text) && confidence >= 40.0f)
                          {
                              string cleanedText = text.Trim();
                              bool hasCJK = System.Text.RegularExpressions.Regex.IsMatch(cleanedText, @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}");
                              
                              // Lọc rỗng và nhiễu (1-2 ký tự đặc biệt, không phải chữ cái/số)
                              if (cleanedText.Length <= 2 && !cleanedText.Any(char.IsLetterOrDigit) && !hasCJK)
                              {
                                  continue;
                              }

                              rawLines.Add((cleanedText, rect.X1, rect.Y1, rect.Width, rect.Height));
                          }
                      }
                  } while (iter.Next(PageIteratorLevel.TextLine));

                  // Dynamic Font-based Bubble Clustering Algorithm
                  bool isVertical = actualLang.Contains("jpn") || actualLang.Contains("chi") || actualLang.Contains("kor");

                  var groups = new List<List<(string Text, int X, int Y, int W, int H)>>();

                  foreach(var line in rawLines) 
                  {
                      // Lấy Max giữa W và H làm size tham chiếu của block
                      int refSize = Math.Max(line.W, line.H);
                      
                      // Mở rộng hit-box khổng lồ (3.0x font size) đa hướng để hút tất cả text rải rác trong 1 Bong bóng thoại 
                      // Manga bubbles thường cách xa nhau, nên hút mạnh tay rất an toàn.
                      int extensionX = (int)(refSize * 3.0);
                      int extensionY = (int)(refSize * 3.0);

                      // Đảm bảo không quá nhỏ đối với các ảnh độ phân giải lớn
                      extensionX = Math.Max(extensionX, 50);
                      extensionY = Math.Max(extensionY, 50);

                      // Create expanded Hitbox for this line to detect neighbors
                      int lx1 = line.X - extensionX;
                      int ly1 = line.Y - extensionY;
                      int lx2 = line.X + line.W + extensionX;
                      int ly2 = line.Y + line.H + extensionY;

                      var intersected = new List<List<(string Text, int X, int Y, int W, int H)>>();
                      foreach(var g in groups) 
                      {
                          // Actual strict bounds of the group (no padding)
                          int gx1 = g.Min(i => i.X);
                          int gy1 = g.Min(i => i.Y);
                          int gx2 = g.Max(i => i.X + i.W);
                          int gy2 = g.Max(i => i.Y + i.H);

                          // AABB Collision Detection
                          if (lx1 < gx2 && lx2 > gx1 && ly1 < gy2 && ly2 > gy1) 
                          {
                              intersected.Add(g);
                          }
                      }

                      if (intersected.Count == 0) {
                          groups.Add(new List<(string Text, int X, int Y, int W, int H)> { line });
                      } else {
                          var first = intersected.First();
                          first.Add(line);
                          foreach(var other in intersected.Skip(1)) {
                              first.AddRange(other);
                              groups.Remove(other);
                          }
                      }
                  }

                  // Assemble Final Bubbles
                  foreach(var g in groups) 
                  {
                      IEnumerable<(string Text, int X, int Y, int W, int H)> sortedGroup;
                      
                      // Manga CJK (Japanese/Chinese/Korean) Vertical Reading Order: 
                      // 1. Phải sang Trái (Right to Left) -> X giảm dần
                      // 2. Trên xuống Dưới (Top to Bottom) -> Y tăng dần
                      if (isVertical) 
                      {
                          int avgW = (int)g.Average(i => i.W);
                          if (avgW < 5) avgW = 5;
                          // Gộp các block trên cùng 1 cột (chia X cho avgW * 1.5 để khoanh vùng cột)
                          sortedGroup = g.OrderByDescending(i => i.X / (int)(avgW * 1.5))
                                         .ThenBy(i => i.Y);
                      } 
                      else 
                      {
                          int avgH = (int)g.Average(i => i.H);
                          if (avgH < 5) avgH = 5;
                          // Standard Horizontal Reading Order: Trái sang Phải, Trên xuống Dưới
                          sortedGroup = g.OrderBy(i => i.Y / (int)(avgH * 1.5))
                                         .ThenBy(i => i.X);
                      }

                      string combinedText = string.Join(" ", sortedGroup.Select(i => i.Text));
                      bool hasCJK = System.Text.RegularExpressions.Regex.IsMatch(combinedText, @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}");
                      
                      if (hasCJK) combinedText = combinedText.Replace(" ", ""); // Không dùng dấu cách cho CJK

                      if (string.IsNullOrWhiteSpace(combinedText)) continue;
                      
                      // Heuristic khử nhiễu speedlines đọc thành tiếng Anh trong manga Nhật/Hàn/Trung
                      if (isVertical && !hasCJK) 
                      {
                         // Nếu dịch tiếng Á mà OCR ra toàn chữ giun dế Latin (thường là rác speedline)
                         // Vẫn cho qua nếu độ dài > 5 (có thể là tên website hợp lệ, watermark tiếng Anh)
                         if (combinedText.Length <= 5) continue;
                      }

                      int minX = g.Min(i => i.X);
                      int minY = g.Min(i => i.Y);
                      int maxX = g.Max(i => i.X + i.W);
                      int maxY = g.Max(i => i.Y + i.H);
                      int width = maxX - minX;
                      int height = maxY - minY;

                      if (width < 25 && height < 25) continue;
                      double aspectRatio = (double)width / height;
                      if (aspectRatio > 25.0 || aspectRatio < 0.04) continue;

                      targetRegions.Add(new Application.Models.OCR.OCRRegion
                      {
                          Text = combinedText,
                          X = Math.Round(((double)minX / imageWidth) * 100, 2),
                          Y = Math.Round(((double)minY / imageHeight) * 100, 2),
                          Width = Math.Round(((double)width / imageWidth) * 100, 2),
                          Height = Math.Round(((double)height / imageHeight) * 100, 2)
                      });
                  }
              }

              using (var img = Pix.LoadFromMemory(pngBytes))
              {
                  ExtractBlocks(img, regions);
              }

              // Fallback: Nếu không tìm thấy chữ nền trắng chữ đen -> thử nền đen chữ trắng
              int totalChars = regions.Sum(r => r.Text.Length);
              if (totalChars < 25)
              {
                  _logger.LogInformation($"Normal OCR found only {totalChars} chars. Inverting colors... (Otsu Inverse)");
                  var invertedRegions = new List<Application.Models.OCR.OCRRegion>();
                  byte[] invertedBytes = PreprocessImageWithOpenCV(imageBytes, invert: true);
                  
                  using (var invertedImgPix = Pix.LoadFromMemory(invertedBytes))
                  {
                      ExtractBlocks(invertedImgPix, invertedRegions);
                  }
                  
                  int invertedChars = invertedRegions.Sum(r => r.Text.Length);
                  if (invertedChars >= totalChars) 
                  {
                     regions = invertedRegions;
                  }
              }
          }

          return regions;
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error extracting text regions with Tesseract OCR.");
          throw;
        }
      });
    }
  }
}
