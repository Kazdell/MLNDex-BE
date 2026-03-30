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
using SixLabors.ImageSharp.Processing;

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

    public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng")
    {
      languageCode = MapToTesseractCode(languageCode);
      return await Task.Run(() =>
      {
        try
        {
          byte[] pngBytes;
          using (var memStream = new MemoryStream(imageBytes))
          {
            using var image = Image.Load(memStream);
            image.Mutate(x => x
               .AutoOrient()
               .Grayscale()
               .Contrast(1.5f));
               
            using var outStream = new MemoryStream();
            image.SaveAsPng(outStream);
            pngBytes = outStream.ToArray();
          }

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
          byte[] pngBytes;
          using var memStreamStream = new MemoryStream(imageBytes);
          using var image = Image.Load(memStreamStream);
          // 1. AI-like Preprocessing: Flatten colors and boost contrast to isolate text bubbles natively
          image.Mutate(x => x
            .AutoOrient()
            .Grayscale()
            .Contrast(1.5f));
          
          int imageWidth = image.Width;
          int imageHeight = image.Height;
          using var outStream = new MemoryStream();
          image.SaveAsPng(outStream);
          pngBytes = outStream.ToArray();

          // Santize and map language code
          var actualLang = languageCode;
          
          if (languageCode.Contains("jpn"))
          {
              if (File.Exists(Path.Combine(_dataPath, "jpn_vert.traineddata")))
                 actualLang = "jpn_vert+jpn"; // Prioritize vertical japanese, fallback to normal japanese
              else
                 actualLang = "jpn"; 
          }
          else if (languageCode.Contains("kor"))
          {
              actualLang = "kor";
          }
          else if (languageCode.Contains("chi_sim"))
          {
              actualLang = "chi_sim"; // Strictly Chinese ONLY
          }
          
          TesseractEngine engine;
          try 
          {
              // LstmOnly prevents fallback to eng if the model doesn't have legacy support
              engine = new TesseractEngine(_dataPath, actualLang, EngineMode.LstmOnly);
          }
          catch (Exception initEx)
          {
              _logger.LogWarning(initEx, "Failed to initialize Tesseract with {Lang}. Falling back to 'eng'", actualLang);
              engine = new TesseractEngine(_dataPath, "eng", EngineMode.LstmOnly);
          }

          using (engine)
          {
              void ExtractBlocks(Pix imgPix)
          {
              // SparseText is MUCH better for finding text in scattered comic bubbles than Auto
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
                  
                  // 1. Confidence & Noise Filter
                  if (!string.IsNullOrWhiteSpace(text) && confidence >= 65.0f)
                  {
                    string cleanedText = text.Trim();
                    // Drop tiny garbage lines (like %U, \\, 2) that don't contain any CJK characters if they are very short
                    bool hasCJK = System.Text.RegularExpressions.Regex.IsMatch(cleanedText, @"\p{IsCJKUnifiedIdeographs}|\p{IsHiragana}|\p{IsKatakana}");
                    if (cleanedText.Length <= 2 && !hasCJK)
                    {
                        continue;
                    }

                    rawLines.Add((cleanedText, rect.X1, rect.Y1, rect.Width, rect.Height));
                  }
                }
              } while (iter.Next(PageIteratorLevel.TextLine));

              // 2. Dynamic Font-based Bubble Clustering Algorithm
              bool isVertical = actualLang.Contains("jpn");
              
              var groups = new List<List<(string Text, int X, int Y, int W, int H)>>();

              foreach(var line in rawLines) 
              {
                  // Font size assumption based on text orientation
                  int fontSize = isVertical ? line.W : line.H;
                  
                  // Dynamic gap distance relative to font size
                  int extensionX = isVertical ? (int)(fontSize * 1.8) : (int)(fontSize * 0.8);
                  int extensionY = isVertical ? (int)(fontSize * 0.8) : (int)(fontSize * 1.5);

                  // Floor limits to prevent tiny anomalies from refusing to cluster
                  extensionX = Math.Max(extensionX, 20);
                  extensionY = Math.Max(extensionY, 20);

                  // Create expanded Hitbox for this line
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

                      // AABB Collision Detection (Line Hitbox vs Group Core bounds)
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

              // 3. Assemble Final Bubbles with Strict Area & Aspect Ratio Rules
              foreach(var g in groups) 
              {
                  string combinedText = string.Join(string.Empty, g.Select(i => i.Text)).Replace(" ", ""); // Chinese has no spaces
                  if (combinedText.Length == 0) continue;

                  int minX = g.Min(i => i.X);
                  int minY = g.Min(i => i.Y);
                  int maxX = g.Max(i => i.X + i.W);
                  int maxY = g.Max(i => i.Y + i.H);
                  int width = maxX - minX;
                  int height = maxY - minY;

                  // Rule A: Minimum dimension to avoid micro-specks being treated as bubbles
                  if (width < 25 && height < 25) continue;

                  // Rule B: Aspect Ratio (Reject incredibly thin lines like swords or floor tiles)
                  double aspectRatio = (double)width / height;
                  if (aspectRatio > 25.0 || aspectRatio < 0.04) continue;

                  regions.Add(new Application.Models.OCR.OCRRegion
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
              ExtractBlocks(img);
          }

          // Fallback: If too few characters found, try inverting colors (white-on-black text)
          int totalChars = regions.Sum(r => r.Text.Length);
          if (totalChars < 25)
          {
              _logger.LogInformation($"Normal OCR found only {totalChars} chars. Inverting colors to detect white-on-black text...");
              var normalRegions = regions.ToList();
              regions.Clear();
              
              image.Mutate(x => x.Invert());
              using var invertedStream = new MemoryStream();
              image.SaveAsPng(invertedStream);
              using (var invertedImgPix = Pix.LoadFromMemory(invertedStream.ToArray()))
              {
                  ExtractBlocks(invertedImgPix);
              }
              
              int invertedChars = regions.Sum(r => r.Text.Length);
              if (invertedChars < totalChars) 
              {
                 regions = normalRegions;
              }
          }
          } // CLOSE using (engine)

          // Bubble creation is now fully resolved inside ExtractBlocks via Dynamic Font Clustering

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
