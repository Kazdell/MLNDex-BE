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

namespace Infrastructure.Services.OCR
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

    // Map ISO 639-1 codes to Tesseract codes. Existing Tesseract codes pass through unchanged.
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

    // Create TesseractEngine with fallback to 'eng' on failure. Always logs the fallback.
    private TesseractEngine CreateTesseractEngine(string language)
    {
        try
        {
            return new TesseractEngine(_dataPath, language, EngineMode.LstmOnly);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Tesseract with '{Lang}'. Falling back to 'eng'", language);
            return new TesseractEngine(_dataPath, "eng", EngineMode.LstmOnly);
        }
    }

    // Resolve vertical traineddata for CJK languages (jpn→jpn_vert+jpn, etc.)
    private string ResolveVerticalLanguage(string languageCode)
    {
        // Handle multi-lang strings like "eng+jpn+chi_sim" — resolve each part independently
        if (languageCode.Contains('+'))
        {
            var parts = languageCode.Split('+');
            var resolved = parts.Select(p => ResolveSingleVerticalLanguage(p));
            return string.Join("+", resolved.Distinct());
        }

        return ResolveSingleVerticalLanguage(languageCode);
    }

    private string ResolveSingleVerticalLanguage(string singleLang)
    {
        var mappings = new[]
        {
            ("jpn", "jpn_vert"),
            ("kor", "kor_vert"),
            ("chi_sim", "chi_sim_vert"),
            ("chi_tra", "chi_tra_vert")
        };

        foreach (var (baseCode, vertCode) in mappings)
        {
            if (singleLang.Contains(baseCode))
            {
                return File.Exists(Path.Combine(_dataPath, $"{vertCode}.traineddata"))
                    ? $"{vertCode}+{baseCode}"
                    : baseCode;
            }
        }

        return singleLang;
    }

    // Preprocess image: Otsu binarization + furigana removal. Returns (pngBytes, width, height) in single decode.
    private (byte[] PngBytes, int OriginalWidth, int OriginalHeight) PreprocessImageWithOpenCV(byte[] imageBytes, bool invert = false)
    {
        using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        if (src.Empty()) return (imageBytes, 0, 0); // fallback

        // Capture original dimensions from this single decode
        int originalWidth = src.Width;
        int originalHeight = src.Height;

        using var gray = new Mat();
        Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);

        using var binarized = new Mat();
        // Otsu Thresholding: auto-find optimal threshold for white bg / black text (manga standard)
        Cv2.Threshold(gray, binarized, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

        // --- FURIGANA REMOVAL (OpenCV Sandpaper) ---
        using var invertedForContours = new Mat();
        Cv2.BitwiseNot(binarized, invertedForContours);

        // Dynamic threshold: scale with image resolution instead of hardcoded 14px
        // ~1.4% of image width, min 10px, max 30px
        int furiganaThreshold = Math.Clamp(originalWidth * 14 / 1000, 10, 30);

        Cv2.FindContours(invertedForContours, out OpenCvSharp.Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        foreach (var contour in contours)
        {
            var rect = Cv2.BoundingRect(contour);
            if (rect.Width < furiganaThreshold && rect.Height < furiganaThreshold) 
            {
                Cv2.DrawContours(binarized, new[] { contour }, -1, Scalar.White, -1);
            }
        }
        // -------------------------------------------

        if (invert)
        {
            Cv2.BitwiseNot(binarized, binarized);
        }

        Cv2.ImEncode(".png", binarized, out byte[] pngBytes);
        return (pngBytes, originalWidth, originalHeight);
    }

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng")
    {
      languageCode = MapToTesseractCode(languageCode);
      languageCode = ResolveVerticalLanguage(languageCode);
      return await Task.Run(() =>
      {
        try
        {
          var (pngBytes, _, _) = PreprocessImageWithOpenCV(imageBytes);

          using var engine = CreateTesseractEngine(languageCode);
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

        private string ExtractBestTextFromCrop(Mat src, OpenCvSharp.Rect roi, string actualLang, TesseractEngine engine)
        {
            var isCJK = actualLang.Contains("jpn") || actualLang.Contains("kor") || actualLang.Contains("chi");
            var psmsToTry = isCJK 
                ? new[] { PageSegMode.SingleBlockVertText, PageSegMode.SingleBlock, PageSegMode.SparseText, PageSegMode.Auto, PageSegMode.SingleLine }
                : new[] { PageSegMode.SingleBlock, PageSegMode.SparseText, PageSegMode.Auto, PageSegMode.SingleLine };

            string bestText = string.Empty;
            Func<string, int> getCjkCount = (str) => System.Text.RegularExpressions.Regex.Matches(str ?? "", @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}|\p{IsHangulSyllables}").Count;

            using var cropped = new Mat(src, roi);
            using var gray = new Mat();
            Cv2.CvtColor(cropped, gray, ColorConversionCodes.BGR2GRAY);
            using var binarized = new Mat();
            // Use Otsu auto-threshold (same as PreprocessImageWithOpenCV) for consistent quality
            Cv2.Threshold(gray, binarized, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            Cv2.ImEncode(".png", binarized, out byte[] pngBytes);

            void ProcessAttempt(byte[] imageToProcess)
            {
                foreach (var psm in psmsToTry)
                {
                    try
                    {
                        using var img = Pix.LoadFromMemory(imageToProcess);
                        using var page = engine.Process(img, psm);
                        var text = page.GetText()?.Trim() ?? string.Empty;

                        if (isCJK)
                        {
                            int currentCjk = getCjkCount(text);
                            int bestCjk = getCjkCount(bestText);
                            double ratio = text.Length > 0 ? (double)currentCjk / text.Length : 0;

                            if (currentCjk > bestCjk) 
                            {
                                if (ratio >= 0.3 || bestCjk == 0) bestText = text;
                            }
                            else if (currentCjk > 0 && currentCjk == bestCjk)
                            {
                                if (text.Length < bestText.Length) bestText = text;
                            }
                        }
                        else
                        {
                            if (text.Length > bestText.Length) bestText = text;
                        }
                    } 
                    catch { /* ignore mode failure */ }
                }
            }

            ProcessAttempt(pngBytes);

            if (string.IsNullOrWhiteSpace(bestText) || (isCJK && getCjkCount(bestText) == 0))
            {
                Cv2.BitwiseNot(binarized, binarized);
                Cv2.ImEncode(".png", binarized, out byte[] invertedPng);
                ProcessAttempt(invertedPng);
            }

            if (!string.IsNullOrWhiteSpace(bestText))
            {
               if (isCJK && getCjkCount(bestText) > 0) 
                   bestText = bestText.Replace(" ", "").Replace("\r", "").Replace("\n", ""); 
               else 
                   bestText = bestText.Replace("\r", "").Replace("\n", " ");
            }

            return bestText ?? string.Empty;
        }

    public async Task<List<Application.Models.OCR.OCRRegion>> ExtractTextRegionsFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng")
    {
      languageCode = MapToTesseractCode(languageCode);
      return await Task.Run(() =>
      {
        var regions = new List<Application.Models.OCR.OCRRegion>();
        try
        {
          // Preprocess + extract dimensions in single decode (no double-decode)
          var (pngBytes, imageWidth, imageHeight) = PreprocessImageWithOpenCV(imageBytes);

          if (imageWidth == 0 || imageHeight == 0)
          {
              _logger.LogWarning("Failed to decode image (0x0 dimensions). Returning empty regions.");
              return regions;
          }

          var actualLang = ResolveVerticalLanguage(languageCode);
          
          // Clean dispose pattern: using declaration ensures disposal in all paths
          using var engine = CreateTesseractEngine(actualLang);

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
                              bool hasCJK = System.Text.RegularExpressions.Regex.IsMatch(cleanedText, @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}|\p{IsHangulSyllables}");
                              
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
                      int extensionX = Math.Max((int)(refSize * 3.0), 50);
                      int extensionY = Math.Max((int)(refSize * 3.0), 50);

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
                      bool hasCJK = System.Text.RegularExpressions.Regex.IsMatch(combinedText, @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}|\p{IsHangulSyllables}");
                      
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

              int totalChars = regions.Sum(r => r.Text.Length);
              if (totalChars < 25)
              {
                  _logger.LogInformation($"Normal OCR found only {totalChars} chars. Inverting colors... (Otsu Inverse)");
                  var invertedRegions = new List<Application.Models.OCR.OCRRegion>();
                  var (invertedBytes, _, _) = PreprocessImageWithOpenCV(imageBytes, invert: true);
                  
                  using (var invertedImgPix = Pix.LoadFromMemory(invertedBytes))
                  {
                      ExtractBlocks(invertedImgPix, invertedRegions);
                  }
                  
                  int invertedChars = invertedRegions.Sum(r => r.Text.Length);
                  if (invertedChars >= totalChars) 
                  {
                     regions = invertedRegions;
                  }
                  else
                  {
                     _logger.LogInformation("Inverted OCR did not improve results. Keeping original.");
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

    public async Task<string> ExtractTextFromCroppedRegionAsync(
        byte[] imageBytes, double xPct, double yPct, double wPct, double hPct, string languageCode = "auto")
    {
      languageCode = MapToTesseractCode(languageCode);
      return await Task.Run(() =>
      {
        try
        {
          using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
          if (src.Empty()) return string.Empty;

          int imgW = src.Width; int imgH = src.Height;
          int cropX = Math.Max(0, (int)(xPct / 100.0 * imgW));
          int cropY = Math.Max(0, (int)(yPct / 100.0 * imgH));
          int cropW = Math.Min(imgW - cropX, (int)(wPct / 100.0 * imgW));
          int cropH = Math.Min(imgH - cropY, (int)(hPct / 100.0 * imgH));

          if (cropW < 10 || cropH < 10) return string.Empty;

          var actualLang = ResolveVerticalLanguage(languageCode);

          using var engine = CreateTesseractEngine(actualLang);
          var roi = new OpenCvSharp.Rect(cropX, cropY, cropW, cropH);
          return ExtractBestTextFromCrop(src, roi, actualLang, engine);
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error extracting text from cropped region.");
          return string.Empty;
        }
      });
    }
  }
}
