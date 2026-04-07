using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces.Moderation;
using Application.Interfaces.OCR;
using Application.Models.OCR;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using Sdcb.PaddleOCR.Models;
using Sdcb.PaddleOCR.Models.Online;

namespace Infrastructure.Services.OCR
{
  /// <summary>
  /// Triển khai PaddleOCR Engine Native trên C# (.NET 8).
  /// Hỗ trợ Dictionary model tải tự động (Model/Weights download on-the-fly).
  /// Áp dụng Pattern: Lazy Loading & Thread-Safe Access via SemaphoreSlim.
  /// </summary>
  public class PaddleOcrService : IOCRService, ITextRecognitionService, IDisposable
  {
    public string ProviderName => "paddle";

    private readonly ILogger<PaddleOcrService> _logger;

    // Caching Object Pool by Language.
    // Value = (PaddleOcrAll Instance, Lock for thread-safety)
    private readonly ConcurrentDictionary<string, (PaddleOcrAll Engine, SemaphoreSlim Lock)> _ocrEngines = new(StringComparer.OrdinalIgnoreCase);

    public PaddleOcrService(ILogger<PaddleOcrService> logger)
    {
      _logger = logger;
    }

    public async Task<string> RecognizeTextAsync(byte[] croppedImageBytes, string languageCode = "auto")
    {
      if (croppedImageBytes == null || croppedImageBytes.Length == 0)
        return string.Empty;

      var (engine, asyncLock) = await GetOrInitializeEngineAsync(languageCode);

      using var mat = Cv2.ImDecode(croppedImageBytes, ImreadModes.Color);
      if (mat.Empty())
      {
        _logger.LogWarning("[PaddleOCR] Received empty or invalid image byte array.");
        return string.Empty;
      }

      // PaddleOcrAll.Run() is NOT thread-safe in C++. We must synchronize access.
      await asyncLock.WaitAsync();
      try
      {
        // Run full OCR pipeline (det + rec) on the cropped box.
        // Because it's already a single box, this acts directly as Text Recognition.
        var result = engine.Run(mat);
        return result.Text ?? string.Empty;
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "[PaddleOCR] Native C++ Runtime Exception during Run()");
        throw;
      }
      finally
      {
        asyncLock.Release();
      }
    }

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string languageCode = "auto")
    {
      return await RecognizeTextAsync(imageBytes, languageCode);
    }

    public async Task<string> ExtractTextFromCroppedRegionAsync(byte[] imageBytes, double xPct, double yPct, double wPct, double hPct, string languageCode = "auto")
    {
      // For now, doing full OCR. If needed, we can crop here with OpenCV.
      // As TextRect recognition usually takes pre-cropped image from ReaderTranslationService.
      return await RecognizeTextAsync(imageBytes, languageCode);
    }

    public async Task<List<OCRRegion>> ExtractTextRegionsFromImageAsync(byte[] imageBytes, string languageCode = "auto")
    {
      if (imageBytes == null || imageBytes.Length == 0)
        return new List<OCRRegion>();

      var (engine, asyncLock) = await GetOrInitializeEngineAsync(languageCode);

      using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
      if (mat.Empty()) return new List<OCRRegion>();

      await asyncLock.WaitAsync();
      try
      {
        var result = engine.Run(mat);
        return result.Regions.Select(r =>
        {
          var bRect = r.Rect.BoundingRect();
          return new OCRRegion
          {
            Text = r.Text,
            // Convert Quad to standard bounding box
            X = bRect.X / (double)mat.Width * 100,
            Y = bRect.Y / (double)mat.Height * 100,
            Width = bRect.Width / (double)mat.Width * 100,
            Height = bRect.Height / (double)mat.Height * 100
          };
        }).ToList();
      }
      catch (Exception ex)
      {
        _logger.LogError(ex, "[PaddleOCR] Native C++ Runtime Exception during ExtractTextRegionsFromImageAsync");
        throw;
      }
      finally
      {
        asyncLock.Release();
      }
    }

    private async Task<(PaddleOcrAll Engine, SemaphoreSlim Lock)> GetOrInitializeEngineAsync(string languageCode)
    {
      // Normalize language code to standard mapping
      string normalizedLang = languageCode.ToLower() switch
      {
        "ja" or "jp" or "japan" or "japanese" => "japan",
        "ko" or "kr" or "korea" or "korean" => "korean",
        "en" or "eng" or "english" => "en",
        _ => "ch" // Default is ChineseV4 (Supports CH + EN)
      };

      if (_ocrEngines.TryGetValue(normalizedLang, out var existing))
      {
        return existing;
      }

      // Lock on string intern specifically to avoid downloading same model 2x simultaneously
      // (Using Semaphores for fine-grained would be better but this is sufficient for cold-start).
      var initLock = new SemaphoreSlim(1, 1);
      await initLock.WaitAsync();
      try
      {
        if (_ocrEngines.TryGetValue(normalizedLang, out var lateFound))
          return lateFound;

        _logger.LogInformation("[PaddleOCR] Downloading and initializing Online Model for Language: {lang}...", normalizedLang);

        FullOcrModel onlineModel = normalizedLang switch
        {
          "japan" => await OnlineFullModels.JapanV4.DownloadAsync(),
          "korean" => await OnlineFullModels.KoreanV4.DownloadAsync(),
          "en" => await OnlineFullModels.EnglishV4.DownloadAsync(),
          _ => await OnlineFullModels.ChineseV4.DownloadAsync()
        };

        var ocrAll = new PaddleOcrAll(onlineModel)
        {
          AllowRotateDetection = true,
          Enable180Classification = false
        };

        var lockSlim = new SemaphoreSlim(1, 1);
        var tuple = (ocrAll, lockSlim);
        _ocrEngines.TryAdd(normalizedLang, tuple);

        _logger.LogInformation("[PaddleOCR] Initialized Model {lang} successfully in RAM.", normalizedLang);
        return tuple;
      }
      finally
      {
        initLock.Release();
      }
    }

    public void Dispose()
    {
      foreach (var kvp in _ocrEngines)
      {
        kvp.Value.Engine?.Dispose();
        kvp.Value.Lock?.Dispose();
      }
      _ocrEngines.Clear();
    }
  }
}
