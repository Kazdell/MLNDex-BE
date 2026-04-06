using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces.OCR;
using Application.Models.OCR;
using Microsoft.Extensions.Options;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Infrastructure.Services.OCR
{
  public class TextDetectorOnnxService : ITextDetectorService, IDisposable
  {
    private readonly InferenceSession _session;
    private readonly SemaphoreSlim _semaphore;
    private readonly OcrSettings _settings;

    public TextDetectorOnnxService(IOptionsMonitor<OcrSettings> optionsMonitor)
    {
      _settings = optionsMonitor.CurrentValue;
      optionsMonitor.OnChange(newSettings =>
      {
        // Thay đổi tham số realtime khi config cập nhật
        _settings.TextThreshold = newSettings.TextThreshold;
        _settings.LinkKernelSize = newSettings.LinkKernelSize;
      });

      int maxConcurrency = Math.Max(1, Environment.ProcessorCount - 1);
      _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

      string modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "craft_mlt_25k.onnx");

      if (!File.Exists(modelPath))
      {
        throw new FileNotFoundException("OCR model file was not found.", modelPath);
      }

      var options = new SessionOptions();
      try
      {
        options.AppendExecutionProvider_CPU();
      }
      catch { /* Ignore if not supported */ }

      _session = new InferenceSession(modelPath, options);
    }

    public async Task<List<BoundingBoxDto>> DetectTextBoxesAsync(Stream imageStream)
    {
      if (_session == null)
        throw new FileNotFoundException("Model ONNX (CRAFT) chưa được nạp. Hãy kiểm tra thư mục Resources.");

      await _semaphore.WaitAsync();
      try
      {
        using var memoryStream = new MemoryStream();
        await imageStream.CopyToAsync(memoryStream);
        byte[] imageBytes = memoryStream.ToArray();

        return await Task.Run(() => ProcessImage(imageBytes));
      }
      finally
      {
        _semaphore.Release();
      }
    }

    private List<BoundingBoxDto> ProcessImage(byte[] imageBytes)
    {
      var boxes = new List<BoundingBoxDto>();

      // 1. Decode image
      using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
      if (mat.Empty()) return boxes;

      int originalW = mat.Width;
      int originalH = mat.Height;

      // 2. Compute resize dim (Target size mult of 32, max side ~1280)
      int maxSideLength = 1280;
      float ratio = 1.0f;
      if (Math.Max(originalH, originalW) > maxSideLength)
      {
        ratio = (float)maxSideLength / Math.Max(originalH, originalW);
      }

      int reW = (int)(originalW * ratio);
      int reH = (int)(originalH * ratio);
      reW = (reW / 32) * 32;
      reH = (reH / 32) * 32;

      if (reW == 0 || reH == 0) return boxes;

      float ratioW = (float)reW / originalW;
      float ratioH = (float)reH / originalH;

      using var resizedMat = new Mat();
      Cv2.Resize(mat, resizedMat, new Size(reW, reH));

      // Convert BGR (OpenCV default) to RGB
      using var rgbMat = new Mat();
      Cv2.CvtColor(resizedMat, rgbMat, ColorConversionCodes.BGR2RGB);

      // 3. Create Tensor [1, 3, reH, reW]
      var tensor = new DenseTensor<float>(new[] { 1, 3, reH, reW });

      // Standard ImageNet normalization for CRAFT
      float[] mean = { 0.485f, 0.456f, 0.406f };
      float[] std = { 0.229f, 0.224f, 0.225f };

      // Trích xuất pixel vào Tensor (Channel, Y, X)
      unsafe
      {
        byte* pRgb = (byte*)rgbMat.DataPointer;
        int step = (int)rgbMat.Step();

        for (int y = 0; y < reH; y++)
        {
          for (int x = 0; x < reW; x++)
          {
            int offset = y * step + x * 3;
            float r = pRgb[offset] / 255.0f;
            float g = pRgb[offset + 1] / 255.0f;
            float b = pRgb[offset + 2] / 255.0f;

            tensor[0, 0, y, x] = (r - mean[0]) / std[0]; // R
            tensor[0, 1, y, x] = (g - mean[1]) / std[1]; // G
            tensor[0, 2, y, x] = (b - mean[2]) / std[2]; // B
          }
        }
      }

      // 4. Run ONNX Inference
      var expectedInputName = _session.InputMetadata.Keys.FirstOrDefault() ?? "input";
      var inputs = new List<NamedOnnxValue> { NamedOnnxValue.CreateFromTensor(expectedInputName, tensor) };

      using var results = _session.Run(inputs);

      var output = results.First().AsTensor<float>();
      var dims = output.Dimensions;
      // Most CRAFT outputs are [1, H_out, W_out, 2] or [1, 2, H_out, W_out]
      // where H_out = reH/2, W_out = reW/2.
      // Let's deduce layout dynamically.

      int hIndex = 1, wIndex = 2, channelIndex = 3;
      if (dims[1] == 2 || dims[1] == 1) // [1, 2, H, W]
      {
        channelIndex = 1; hIndex = 2; wIndex = 3;
      }

      int outH = dims[hIndex];
      int outW = dims[wIndex];

      // 5. Read Region Score Map into a single-channel Mat
      using var scoreMat = new Mat(outH, outW, MatType.CV_32FC1);
      unsafe
      {
        float* pScore = (float*)scoreMat.DataPointer;
        for (int y = 0; y < outH; y++)
        {
          for (int x = 0; x < outW; x++)
          {
            // CRAFT usually puts Region Score at index 0
            float val = 0;
            if (channelIndex == 3) val = output[0, y, x, 0];
            else val = output[0, 0, y, x];

            pScore[y * outW + x] = val;
          }
        }
      }

      // Convert to 8-bit image for Contour Finding
      using var scoreMat8U = new Mat();
      scoreMat.ConvertTo(scoreMat8U, MatType.CV_8UC1, 255.0);

      // Apply threshold (Text score > settings.TextThreshold)
      float txtThresh = _settings?.TextThreshold ?? 0.45f;
      Cv2.Threshold(scoreMat8U, scoreMat8U, 255 * txtThresh, 255, ThresholdTypes.Binary);

      // GIAI ĐOẠN MỚI: Dilation & Morph Close để gộp các chữ cái rời rạc (character bounding boxes) thành cụm (paragraph/bubble)
      int kernelSize = _settings?.LinkKernelSize ?? 15;
      using var kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(kernelSize, kernelSize));
      Cv2.MorphologyEx(scoreMat8U, scoreMat8U, MorphTypes.Close, kernel);
      Cv2.Dilate(scoreMat8U, scoreMat8U, kernel);

      // 6. Find Contours
      Cv2.FindContours(scoreMat8U, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

      foreach (var contour in contours)
      {
        var rect = Cv2.BoundingRect(contour);

        // CRAFT output is half the size of the input tensor, so multiply by 2
        // Then divide by ratio to get original image coordinates
        int xMain = (int)((rect.X * 2) / ratioW);
        int yMain = (int)((rect.Y * 2) / ratioH);
        int wMain = (int)((rect.Width * 2) / ratioW);
        int hMain = (int)((rect.Height * 2) / ratioH);

        // Add small padding (e.g., 5px) to safely wrap characters
        int padding = 5;
        xMain = Math.Max(0, xMain - padding);
        yMain = Math.Max(0, yMain - padding);
        wMain = Math.Min(originalW - xMain, wMain + padding * 2);
        hMain = Math.Min(originalH - yMain, hMain + padding * 2);

        // Filter tiny noise boxes
        if (wMain < 10 || hMain < 10) continue;

        boxes.Add(new BoundingBoxDto
        {
          X = xMain,
          Y = yMain,
          Width = wMain,
          Height = hMain
        });
      }

      return boxes;
    }

    public void Dispose()
    {
      _session?.Dispose();
      _semaphore?.Dispose();
    }
  }
}
