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
using Application.Interfaces.OCR;

namespace Infrastructure.Services.OCR
{
  public class TesseractOCRService : IOCRService, ITextRecognitionService
  {
    private readonly string _dataPath; // Path to tessdata directory
    private readonly ILogger<TesseractOCRService> _logger;
    private readonly ITextDetectorService _textDetector;
    private readonly IImagePreprocessorService _imagePreprocessor;

    public string ProviderName => "tesseract";

    public TesseractOCRService(
        ILogger<TesseractOCRService> logger,
        ITextDetectorService textDetector,
        IImagePreprocessorService imagePreprocessor)
    {
      _logger = logger;
      _textDetector = textDetector;
      _imagePreprocessor = imagePreprocessor;
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
        "zh" => "chi_sim",
        "ja" => "jpn",
        "ko" => "kor",
        "en" => "eng",
        "vi" => "vie",
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
          ? new[] { PageSegMode.SingleBlockVertText, PageSegMode.SingleBlock, PageSegMode.SingleLine }
          : new[] { PageSegMode.SingleBlock, PageSegMode.SingleLine };

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
      var actualLang = ResolveVerticalLanguage(languageCode);

      try
      {
        using var ms = new MemoryStream(imageBytes);

        // Bước 1: Gọi thuật toán CRAFT AI để vẽ Bounding Box
        var boxes = await _textDetector.DetectTextBoxesAsync(ms);

        if (boxes == null || boxes.Count == 0)
        {
          _logger.LogWarning("TextDetector did not find any boxes. Returning empty regions.");
          return new List<Application.Models.OCR.OCRRegion>();
        }

        ms.Position = 0; // IMPORTANT: Reset stream position after it was read by TextDetector

        // Bước 2: Gọi OpenCV để cắt từng box rời rạc, tẩy trắng, và tăng cường tương phản
        var croppedStreams = await _imagePreprocessor.CutAndCleanBoxesAsync(ms, boxes);

        var regions = new List<Application.Models.OCR.OCRRegion>();

        // Decode ảnh tạm chỉ để lấy chiều Rộng, Cao chuẩn (phục vụ tính toán Tọa độ %)
        using var src = Cv2.ImDecode(imageBytes, ImreadModes.Color);
        int imageWidth = src.Width;
        int imageHeight = src.Height;

        // Bước 3: Nạp ảnh cắt vào Tesseract Engine và đẩy Data
        using var engine = CreateTesseractEngine(actualLang);

        for (int i = 0; i < boxes.Count; i++)
        {
          var cropStream = croppedStreams[i];
          byte[] cropBytes = ((MemoryStream)cropStream).ToArray();

          var psmBestText = string.Empty;
          bool isCJK = actualLang.Contains("jpn") || actualLang.Contains("kor") || actualLang.Contains("chi");
          var psmsToTry = isCJK
              ? new[] { PageSegMode.SingleBlockVertText, PageSegMode.SingleBlock }
              : new[] { PageSegMode.SingleBlock };

          Func<string, int> getCjkCount = (str) => System.Text.RegularExpressions.Regex.Matches(str ?? "", @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}|\p{IsHangulSyllables}").Count;

          using var img = Pix.LoadFromMemory(cropBytes);

          foreach (var psm in psmsToTry)
          {
            try
            {
              using var page = engine.Process(img, psm);
              string t = page.GetText()?.Trim() ?? string.Empty;

              if (isCJK)
              {
                int currentCjk = getCjkCount(t);
                int bestCjk = getCjkCount(psmBestText);
                double ratio = t.Length > 0 ? (double)currentCjk / t.Length : 0;

                if (currentCjk > bestCjk)
                {
                  if (ratio >= 0.3 || bestCjk == 0) psmBestText = t;
                }
                else if (currentCjk > 0 && currentCjk == bestCjk && t.Length < psmBestText.Length)
                {
                  psmBestText = t;
                }
              }
              else
              {
                if (t.Length > psmBestText.Length) psmBestText = t;
              }
            }
            catch { /* ignore mode failure */ }
          }

          var text = psmBestText;

          bool isVertical = actualLang.Contains("jpn") || actualLang.Contains("chi") || actualLang.Contains("kor");

          if (!string.IsNullOrWhiteSpace(text))
          {
            string cleanedText = text;
            bool hasCJK = getCjkCount(cleanedText) > 0;

            if (hasCJK)
            {
              cleanedText = cleanedText.Replace(" ", "").Replace("\r", "").Replace("\n", "");
            }
            else
            {
              cleanedText = cleanedText.Replace("\r", "").Replace("\n", " ");
            }

            // Heuristic khử nhiễu speedlines đới với truyện dọc
            if (isVertical && !hasCJK)
            {
              if (cleanedText.Length <= 5) continue;
            }

            regions.Add(new Application.Models.OCR.OCRRegion
            {
              Text = cleanedText,
              X = Math.Round((boxes[i].X / (double)imageWidth) * 100, 2),
              Y = Math.Round((boxes[i].Y / (double)imageHeight) * 100, 2),
              Width = Math.Round((boxes[i].Width / (double)imageWidth) * 100, 2),
              Height = Math.Round((boxes[i].Height / (double)imageHeight) * 100, 2)
            });
          }
        }

        return regions;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "Error extracting text regions with CRAFT + Tesseract Pipeline.");
        throw;
      }
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

    public Task<string> RecognizeTextAsync(byte[] croppedImageBytes, string languageCode = "auto")
    {
      return ExtractTextFromCroppedRegionAsync(croppedImageBytes, 0, 0, 100, 100, languageCode);
    }
  }
}
