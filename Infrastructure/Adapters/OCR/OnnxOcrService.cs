using Application.Interfaces.Moderation;
using Application.Models.OCR;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Adapters.OCR
{
    /// <summary>
    /// ONNX-based OCR service using comic-text-detector (detection) + PaddleOCR CRNN (recognition).
    /// Implements IOCRService for seamless DI swap with TesseractOCRService.
    /// Falls back gracefully if model files are missing.
    /// </summary>
    public class OnnxOcrService : IOCRService, IDisposable
    {
        private readonly ILogger<OnnxOcrService> _logger;
        private readonly InferenceSession? _detSession;

        // Multi-language recognition models
        private readonly Dictionary<string, RecLanguageModel> _recModels = new();
        private const string DefaultLang = "cjk"; // Chinese/Japanese/English default

        // Detection constants
        private const int DetInputSize = 1024;
        private const float ConfidenceThreshold = 0.4f;
        private const float NmsIouThreshold = 0.45f;

        // Recognition constants
        private const int RecTargetHeight = 48;
        private const int RecMaxWidth = 320;

        /// <summary>
        /// Holds an ONNX recognition session + its character dictionary for a specific language.
        /// </summary>
        private sealed class RecLanguageModel : IDisposable
        {
            public InferenceSession Session { get; }
            public string[] Dictionary { get; }
            public string Label { get; }

            public RecLanguageModel(InferenceSession session, string[] dictionary, string label)
            {
                Session = session;
                Dictionary = dictionary;
                Label = label;
            }

            public void Dispose() => Session.Dispose();
        }

        /// <summary>
        /// Indicates whether the detection model + at least one recognition model are loaded.
        /// </summary>
        public bool IsAvailable { get; }

        /// <summary>
        /// IOCRService provider identifier
        /// </summary>
        public string ProviderName => "onnx";

        public OnnxOcrService(ILogger<OnnxOcrService> logger)
        {
            _logger = logger;

            var modelDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models");
            string detPath = Path.Combine(modelDir, "comictextdetector.pt.onnx");

            // Recognition model registry: (key, onnxFile, dictFile, label)
            var recModelDefs = new[]
            {
                (key: "cjk",    onnx: "ch_PP-OCRv4_rec.onnx", dict: "ppocr_keys_v1.txt",  label: "Chinese/Japanese/English"),
                (key: "korean", onnx: "korean_rec.onnx",       dict: "korean_dict.txt",    label: "Korean"),
            };

            if (!File.Exists(detPath))
            {
                _logger.LogWarning("⚠️ Detection model not found: {Path}. OnnxOcrService disabled.", detPath);
                IsAvailable = false;
                return;
            }

            try
            {
                var sessionOptions = new SessionOptions
                {
                    GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL
                };

                _detSession = new InferenceSession(detPath, sessionOptions);

                // Load each recognition model if files exist
                foreach (var def in recModelDefs)
                {
                    string recPath = Path.Combine(modelDir, def.onnx);
                    string dictPath = Path.Combine(modelDir, def.dict);

                    if (File.Exists(recPath) && File.Exists(dictPath))
                    {
                        var recSession = new InferenceSession(recPath, sessionOptions);
                        var dictionary = File.ReadAllLines(dictPath);
                        _recModels[def.key] = new RecLanguageModel(recSession, dictionary, def.label);
                        _logger.LogInformation("  ✅ Loaded rec model [{Key}]: {Label} ({DictSize} chars)",
                            def.key, def.label, dictionary.Length);
                    }
                    else
                    {
                        _logger.LogWarning("  ⚠️ Rec model [{Key}] skipped: {Onnx} or {Dict} not found",
                            def.key, def.onnx, def.dict);
                    }
                }

                IsAvailable = _recModels.Count > 0;

                if (IsAvailable)
                {
                    _logger.LogInformation("✅ ONNX OCR Engine ready: Detection + {Count} recognition models [{Langs}]",
                        _recModels.Count, string.Join(", ", _recModels.Keys));
                }
                else
                {
                    _logger.LogWarning("⚠️ No recognition models loaded. OnnxOcrService disabled.");
                    _detSession?.Dispose();
                    _detSession = null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to load ONNX models. OnnxOcrService disabled.");
                IsAvailable = false;
                _detSession?.Dispose();
                _detSession = null;
                foreach (var m in _recModels.Values) m.Dispose();
                _recModels.Clear();
            }
        }

        /// <summary>
        /// Resolve ISO 639-1 language code to recognition model key.
        /// Routes: ko → "korean" model | zh, ja, en, vi, auto → "cjk" model
        /// Note: Japanese (ja) uses CJK model (β Beta — shared model, not dedicated).
        /// </summary>
        private string ResolveRecModelKey(string languageCode)
        {
            var lang = languageCode.ToLowerInvariant().Trim();

            // Korean → dedicated korean model
            if (lang == "ko" || lang.Contains("kor") || lang.Contains("kr") || lang.Contains("korean"))
                return _recModels.ContainsKey("korean") ? "korean" : DefaultLang;

            // Chinese (zh), Japanese (ja/β), English (en), Vietnamese (vi), Auto
            // All route to CJK model (ch_PP-OCRv4_rec.onnx)
            // Japanese: CJK model handles Kanji but hiragana/katakana accuracy is limited
            // Vietnamese: CJK model has limited diacritics support
            return DefaultLang;
        }

        // ============================================================
        // IOCRService INTERFACE IMPLEMENTATIONS
        // ============================================================

        public async Task<string> ExtractTextFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng")
        {
            var regions = await ExtractTextRegionsFromImageAsync(imageBytes, languageCode);
            return string.Join("\n", regions.Select(r => r.Text));
        }

        public async Task<List<OCRRegion>> ExtractTextRegionsFromImageAsync(byte[] imageBytes, string languageCode = "vie+eng")
        {
            if (!IsAvailable || _detSession == null || _recModels.Count == 0)
            {
                throw new InvalidOperationException("OnnxOcrService is not available. Model files may be missing.");
            }

            // Resolve which recognition model to use
            var recKey = ResolveRecModelKey(languageCode);
            if (!_recModels.TryGetValue(recKey, out var recModel))
                recModel = _recModels.Values.First(); // fallback to any available

            return await Task.Run(() =>
            {
                try
                {
                    // Load image with OpenCV
                    using var mat = Mat.FromImageData(imageBytes, ImreadModes.Color);
                    int origW = mat.Width;
                    int origH = mat.Height;

                    _logger.LogInformation("ONNX OCR: Processing image {W}x{H}", origW, origH);

                    // ── STEP 1: Detect text boxes ──
                    var boxes = DetectTextBoxes(mat, origW, origH);
                    _logger.LogInformation("ONNX OCR: Detected {Count} text boxes after NMS", boxes.Count);

                    if (boxes.Count == 0)
                        return new List<OCRRegion>();

                    // ── STEP 2: Recognize text in each box ──
                    var texts = RecognizeText(mat, boxes, recModel);
                    _logger.LogInformation("ONNX OCR: Recognized {Count} text regions using [{Lang}] model", texts.Count, recModel.Label);

                    // ── STEP 3: Assemble OCRRegions with percentage coordinates ──
                    var regions = new List<OCRRegion>();
                    for (int i = 0; i < boxes.Count; i++)
                    {
                        var box = boxes[i];
                        string text = i < texts.Count ? texts[i] : string.Empty;

                        if (string.IsNullOrWhiteSpace(text))
                            continue;

                        regions.Add(new OCRRegion
                        {
                            Text = text.Trim(),
                            X = Math.Round(((double)box.X / origW) * 100, 2),
                            Y = Math.Round(((double)box.Y / origH) * 100, 2),
                            Width = Math.Round(((double)box.Width / origW) * 100, 2),
                            Height = Math.Round(((double)box.Height / origH) * 100, 2)
                        });
                    }

                    return regions;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ONNX OCR: Error during inference pipeline");
                    throw;
                }
            });
        }

        // ============================================================
        // STEP 1: TEXT DETECTION (comic-text-detector)
        // ============================================================

        private List<Rect> DetectTextBoxes(Mat originalMat, int origW, int origH)
        {
            // 1. Resize to model input size (1024x1024)
            using var resized = new Mat();
            Cv2.Resize(originalMat, resized, new Size(DetInputSize, DetInputSize));

            // 2. Convert BGR→RGB and build NCHW float tensor
            using var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            var inputTensor = new DenseTensor<float>(new[] { 1, 3, DetInputSize, DetInputSize });
            var indexer = rgb.GetGenericIndexer<Vec3b>();

            for (int y = 0; y < DetInputSize; y++)
            {
                for (int x = 0; x < DetInputSize; x++)
                {
                    var pixel = indexer[y, x];
                    inputTensor[0, 0, y, x] = pixel.Item0 / 255.0f; // R
                    inputTensor[0, 1, y, x] = pixel.Item1 / 255.0f; // G
                    inputTensor[0, 2, y, x] = pixel.Item2 / 255.0f; // B
                }
            }

            // 3. Run detection model
            var inputName = _detSession!.InputNames[0];
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            using var results = _detSession.Run(inputs);
            var outputTensor = results.First().AsTensor<float>();

            // 4. Parse detections and apply NMS
            var rawBoxes = new List<(float x1, float y1, float x2, float y2, float conf)>();
            var shape = outputTensor.Dimensions;

            // Output shape varies by model, handle [1, N, 6] or [N, 6]
            int numDetections;
            int startDim;
            if (shape.Length == 3)
            {
                numDetections = shape[1];
                startDim = 1;
            }
            else
            {
                numDetections = shape[0];
                startDim = 0;
            }

            for (int i = 0; i < numDetections; i++)
            {
                float conf;
                float x1, y1, x2, y2;

                if (startDim == 1)
                {
                    x1 = outputTensor[0, i, 0];
                    y1 = outputTensor[0, i, 1];
                    x2 = outputTensor[0, i, 2];
                    y2 = outputTensor[0, i, 3];
                    conf = outputTensor[0, i, 4];
                }
                else
                {
                    x1 = outputTensor[i, 0];
                    y1 = outputTensor[i, 1];
                    x2 = outputTensor[i, 2];
                    y2 = outputTensor[i, 3];
                    conf = outputTensor[i, 4];
                }

                if (conf >= ConfidenceThreshold && x2 > x1 && y2 > y1)
                {
                    rawBoxes.Add((x1, y1, x2, y2, conf));
                }
            }

            // Apply NMS
            var nmsBoxes = ApplyNms(rawBoxes, NmsIouThreshold);

            // Scale back to original image coordinates
            float scaleX = (float)origW / DetInputSize;
            float scaleY = (float)origH / DetInputSize;

            var result = new List<Rect>();
            foreach (var box in nmsBoxes)
            {
                int bx = Math.Max(0, (int)(box.x1 * scaleX));
                int by = Math.Max(0, (int)(box.y1 * scaleY));
                int bx2 = Math.Min(origW, (int)(box.x2 * scaleX));
                int by2 = Math.Min(origH, (int)(box.y2 * scaleY));
                int bw = bx2 - bx;
                int bh = by2 - by;

                // Filter tiny boxes (smaller than 15px in either dimension)
                if (bw >= 15 && bh >= 15)
                {
                    result.Add(new Rect(bx, by, bw, bh));
                }
            }

            return result;
        }

        // ============================================================
        // STEP 2: TEXT RECOGNITION (PaddleOCR CRNN)
        // ============================================================

        private List<string> RecognizeText(Mat originalMat, List<Rect> boxes, RecLanguageModel recModel)
        {
            var texts = new List<string>();

            // Process boxes individually to handle different widths
            foreach (var box in boxes)
            {
                try
                {
                    // 1. Crop the region from original image
                    var safeRect = new Rect(
                        Math.Max(0, box.X),
                        Math.Max(0, box.Y),
                        Math.Min(box.Width, originalMat.Width - Math.Max(0, box.X)),
                        Math.Min(box.Height, originalMat.Height - Math.Max(0, box.Y))
                    );

                    if (safeRect.Width <= 0 || safeRect.Height <= 0)
                    {
                        texts.Add(string.Empty);
                        continue;
                    }

                    using var crop = new Mat(originalMat, safeRect);

                    // 2. Resize to target height, maintaining aspect ratio
                    int targetH = RecTargetHeight;
                    int targetW = (int)(crop.Width * ((double)targetH / crop.Height));
                    targetW = Math.Max(targetW, 10);
                    targetW = Math.Min(targetW, RecMaxWidth);

                    using var resizedCrop = new Mat();
                    Cv2.Resize(crop, resizedCrop, new Size(targetW, targetH));

                    // 3. Convert to RGB float32, normalize: (pixel/255 - 0.5) / 0.5
                    using var rgbCrop = new Mat();
                    Cv2.CvtColor(resizedCrop, rgbCrop, ColorConversionCodes.BGR2RGB);

                    var recTensor = new DenseTensor<float>(new[] { 1, 3, targetH, targetW });
                    var cropIndexer = rgbCrop.GetGenericIndexer<Vec3b>();

                    for (int y = 0; y < targetH; y++)
                    {
                        for (int x = 0; x < targetW; x++)
                        {
                            var pixel = cropIndexer[y, x];
                            recTensor[0, 0, y, x] = (pixel.Item0 / 255.0f - 0.5f) / 0.5f; // R
                            recTensor[0, 1, y, x] = (pixel.Item1 / 255.0f - 0.5f) / 0.5f; // G
                            recTensor[0, 2, y, x] = (pixel.Item2 / 255.0f - 0.5f) / 0.5f; // B
                        }
                    }

                    // 4. Run recognition model (language-specific)
                    var recInputName = recModel.Session.InputNames[0];
                    var recInputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(recInputName, recTensor)
                    };

                    using var recResults = recModel.Session.Run(recInputs);
                    var recOutput = recResults.First().AsTensor<float>();

                    // 5. CTC Greedy Decode with language-specific dictionary
                    string text = CtcGreedyDecode(recOutput, recModel.Dictionary);
                    texts.Add(text);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ONNX OCR: Failed to recognize text in box ({X},{Y},{W},{H})",
                        box.X, box.Y, box.Width, box.Height);
                    texts.Add(string.Empty);
                }
            }

            return texts;
        }

        // ============================================================
        // CTC GREEDY DECODE
        // ============================================================

        private static string CtcGreedyDecode(Tensor<float> logits, string[] dictionary)
        {
            // logits shape: [1, T, num_classes] where T = sequence length
            var dims = logits.Dimensions;
            int T = dims[1];
            int numClasses = dims[2];

            var chars = new List<char>();
            int prevIndex = -1;

            for (int t = 0; t < T; t++)
            {
                // Find argmax across classes for this timestep
                int maxIndex = 0;
                float maxVal = logits[0, t, 0];

                for (int c = 1; c < numClasses; c++)
                {
                    float val = logits[0, t, c];
                    if (val > maxVal)
                    {
                        maxVal = val;
                        maxIndex = c;
                    }
                }

                // CTC rules:
                // - Index 0 = blank token → skip
                // - Same as previous → skip (CTC deduplication)
                if (maxIndex != 0 && maxIndex != prevIndex)
                {
                    // Dictionary index: maxIndex - 1 (index 0 is blank)
                    int dictIndex = maxIndex - 1;
                    if (dictIndex >= 0 && dictIndex < dictionary.Length)
                    {
                        string charStr = dictionary[dictIndex];
                        if (charStr.Length > 0)
                        {
                            chars.Add(charStr[0]);
                        }
                    }
                }

                prevIndex = maxIndex;
            }

            return new string(chars.ToArray());
        }

        // ============================================================
        // NMS (Non-Maximum Suppression)
        // ============================================================

        private static List<(float x1, float y1, float x2, float y2, float conf)> ApplyNms(
            List<(float x1, float y1, float x2, float y2, float conf)> boxes,
            float iouThreshold)
        {
            if (boxes.Count == 0) return boxes;

            // Sort by confidence descending
            var sorted = boxes.OrderByDescending(b => b.conf).ToList();
            var kept = new List<(float x1, float y1, float x2, float y2, float conf)>();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                kept.Add(best);
                sorted.RemoveAt(0);

                sorted = sorted.Where(b => ComputeIoU(best, b) < iouThreshold).ToList();
            }

            return kept;
        }

        private static float ComputeIoU(
            (float x1, float y1, float x2, float y2, float conf) a,
            (float x1, float y1, float x2, float y2, float conf) b)
        {
            float interX1 = Math.Max(a.x1, b.x1);
            float interY1 = Math.Max(a.y1, b.y1);
            float interX2 = Math.Min(a.x2, b.x2);
            float interY2 = Math.Min(a.y2, b.y2);

            float interArea = Math.Max(0, interX2 - interX1) * Math.Max(0, interY2 - interY1);
            float areaA = (a.x2 - a.x1) * (a.y2 - a.y1);
            float areaB = (b.x2 - b.x1) * (b.y2 - b.y1);
            float unionArea = areaA + areaB - interArea;

            return unionArea > 0 ? interArea / unionArea : 0;
        }

        // ============================================================
        // DISPOSE
        // ============================================================

        public void Dispose()
        {
            _detSession?.Dispose();
            foreach (var m in _recModels.Values) m.Dispose();
            _recModels.Clear();
        }
    }
}
