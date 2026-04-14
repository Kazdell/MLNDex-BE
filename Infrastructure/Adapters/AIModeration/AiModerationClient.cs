using Application.DTOs.AIModeration;
using Application.Interfaces.AIModeration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Infrastructure.Adapters.AIModeration
{
    public class AiModerationClient : IAiModerationClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AiModerationClient> _logger;

        // Hybrid scan thresholds
        private const double RESCAN_THRESHOLD = 0.3;      // Score >= 0.3 in any category → trigger per-page rescan
        private const double NOTABLE_SCORE = 0.05;     // Score >= 0.05 → save to perPageResults
        private const int BATCH_SIZE = 10;        // Max images per single API call

        private static readonly HttpClient _localHttpClient = new HttpClient();

        public AiModerationClient(IConfiguration configuration, ILogger<AiModerationClient> logger)
        {
            var apiKey = configuration["OpenAI:ApiKey"]
                ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(120) // Longer timeout for batch requests
            };
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            _logger = logger;
        }

        // ═══════════════════════════════════════════════════════════════════
        // HYBRID SCAN: Batch-first → Per-page rescan only when needed
        // ═══════════════════════════════════════════════════════════════════
        public async Task<AiModerationResultDto> ModerateImagesAsync(IEnumerable<string> imageUrls)
        {
            var urls = imageUrls.ToList();
            _logger.LogInformation("[HybridScan] Bắt đầu kiểm duyệt {Count} ảnh", urls.Count);

            // Single image → skip batch overhead, scan directly
            if (urls.Count <= 1)
            {
                return await ScanSingleImageAsync(urls);
            }

            // ── PHASE 1: Batch scan ────────────────────────────────────────
            var batchResult = await RunBatchScanAsync(urls);

            bool needsRescan = batchResult.Flagged
                || batchResult.CategoryScores.Values.Any(v => v >= RESCAN_THRESHOLD);

            if (!needsRescan)
            {
                _logger.LogInformation(
                    "[HybridScan] Batch scan SẠCH → skip per-page rescan. " +
                    "MaxScore={MaxScore:F4}. API calls saved: {Saved}",
                    batchResult.CategoryScores.Values.DefaultIfEmpty(0).Max(),
                    urls.Count - 1);

                batchResult.ScanMode = "batch_only";
                return batchResult;
            }

            // ── PHASE 2: Per-page rescan ───────────────────────────────────
            _logger.LogWarning(
                "[HybridScan] Batch phát hiện nghi vấn → Rescan từng ảnh. Flagged={Flagged}, MaxScore={MaxScore:F4}",
                batchResult.Flagged,
                batchResult.CategoryScores.Values.DefaultIfEmpty(0).Max());

            var detailedResult = await RunPerPageScanAsync(urls);
            detailedResult.ScanMode = "hybrid";
            return detailedResult;
        }

        // ── Single image shortcut ──────────────────────────────────────────
        private async Task<AiModerationResultDto> ScanSingleImageAsync(List<string> urls)
        {
            if (urls.Count == 0)
            {
                return new AiModerationResultDto
                {
                    Flagged = false,
                    CategoryScores = new Dictionary<string, double>(),
                    ScanMode = "batch_only"
                };
            }

            var result = await ModerateOneImageAsync(urls[0]);
            var dto = new AiModerationResultDto
            {
                CategoryScores = result.Scores,
                Flagged = result.Flagged,
                FlaggedReason = BuildFlaggedReason(result.Scores),
                ScanMode = "batch_only"
            };

            if (result.Flagged || result.Scores.Values.Any(v => v >= NOTABLE_SCORE))
            {
                dto.PerPageResults = new List<PageModerationDto>
                {
                    new()
                    {
                        PageNumber = 1,
                        ImageUrl = urls[0],
                        Flagged = result.Flagged,
                        CategoryScores = result.Scores
                            .Where(s => s.Value >= 0.01)
                            .ToDictionary(s => s.Key, s => s.Value)
                    }
                };
            }

            return dto;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PHASE 1: Batch scan — gom ảnh thành batches, 1 API call per batch
        // ═══════════════════════════════════════════════════════════════════
        private async Task<AiModerationResultDto> RunBatchScanAsync(List<string> urls)
        {
            var maxScores = new Dictionary<string, double>();
            bool overallFlagged = false;

            // Split into batches of BATCH_SIZE
            var batches = urls
                .Select((url, idx) => new { url, idx })
                .Chunk(BATCH_SIZE)
                .ToList();

            _logger.LogInformation("[HybridScan] Phase 1: Batch scan — {BatchCount} batch(es) cho {Count} ảnh",
                batches.Count, urls.Count);

            foreach (var batch in batches)
            {
                try
                {
                    var batchUrls = batch.Select(b => b.url).ToList();
                    var batchResults = await ModerateBatchImagesAsync(batchUrls);

                    foreach (var (scores, flagged) in batchResults)
                    {
                        if (flagged) overallFlagged = true;
                        foreach (var kvp in scores)
                        {
                            if (!maxScores.ContainsKey(kvp.Key) || kvp.Value > maxScores[kvp.Key])
                                maxScores[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[HybridScan] Batch scan lỗi — fallback sang per-page scan");
                    // If batch fails, return flagged=true to trigger per-page scan as fallback
                    return new AiModerationResultDto
                    {
                        Flagged = true,
                        CategoryScores = maxScores,
                        FlaggedReason = "batch_error_fallback"
                    };
                }
            }

            return new AiModerationResultDto
            {
                CategoryScores = maxScores,
                Flagged = overallFlagged,
                FlaggedReason = BuildFlaggedReason(maxScores)
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        //  PHASE 2: Per-page rescan — quét từng ảnh riêng lẻ
        // ═══════════════════════════════════════════════════════════════════
        private async Task<AiModerationResultDto> RunPerPageScanAsync(List<string> urls)
        {
            var maxScores = new Dictionary<string, double>();
            bool overallFlagged = false;
            var perPageResults = new List<PageModerationDto>();

            _logger.LogInformation("[HybridScan] Phase 2: Per-page rescan — {Count} ảnh", urls.Count);

            for (int i = 0; i < urls.Count; i++)
            {
                try
                {
                    var result = await ModerateOneImageAsync(urls[i]);
                    foreach (var kvp in result.Scores)
                    {
                        if (!maxScores.ContainsKey(kvp.Key) || kvp.Value > maxScores[kvp.Key])
                            maxScores[kvp.Key] = kvp.Value;
                    }
                    if (result.Flagged) overallFlagged = true;

                    // Save per-page data for flagged or notable-score pages
                    bool hasNotableScore = result.Scores.Values.Any(v => v >= NOTABLE_SCORE);
                    if (result.Flagged || hasNotableScore)
                    {
                        perPageResults.Add(new PageModerationDto
                        {
                            PageNumber = i + 1,
                            ImageUrl = urls[i],
                            Flagged = result.Flagged,
                            CategoryScores = result.Scores
                                .Where(s => s.Value >= 0.01)
                                .ToDictionary(s => s.Key, s => s.Value)
                        });
                    }
                }
                catch (Exception ex)
                {
                    var safeUrl = urls[i].Length > 100 ? urls[i][..100] + "..." : urls[i];
                    _logger.LogError(ex, "[HybridScan] Lỗi khi kiểm duyệt ảnh: {Url}", safeUrl);
                }
            }

            return new AiModerationResultDto
            {
                CategoryScores = maxScores,
                Flagged = overallFlagged,
                FlaggedReason = BuildFlaggedReason(maxScores),
                PerPageResults = perPageResults.Count > 0 ? perPageResults : null
            };
        }

        // ═══════════════════════════════════════════════════════════════════
        //  API Callers
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// Send a batch of images in ONE API call. Returns per-image results.
        /// </summary>
        private async Task<List<(Dictionary<string, double> Scores, bool Flagged)>> ModerateBatchImagesAsync(
            List<string> imageUrls)
        {
            var inputItems = new List<object>();

            foreach (var url in imageUrls)
            {
                string finalUrl = url;
                if (url.Contains("localhost") || url.Contains("127.0.0.1"))
                {
                    try
                    {
                        var bytes = await _localHttpClient.GetByteArrayAsync(url);
                        var base64 = Convert.ToBase64String(bytes);
                        // Assuming jpeg for generic local files, OpenAI can usually infer it from the base64 content
                        finalUrl = $"data:image/jpeg;base64,{base64}";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[HybridScan] Lỗi khi tải ảnh local để convert sang base64: {Url}", url);
                    }
                }

                inputItems.Add(new Dictionary<string, object>
                {
                    ["type"] = "image_url",
                    ["image_url"] = new { url = finalUrl }
                });
            }

            var requestBody = new
            {
                model = "omni-moderation-latest",
                input = inputItems.ToArray()
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await SendRequestWithRetryAsync(content);
            var responseBody = await response.Content.ReadAsStringAsync();

            return ParseMultipleResults(responseBody);
        }


        /// <summary>
        /// Send a single image for moderation.
        /// </summary>
        private async Task<(Dictionary<string, double> Scores, bool Flagged)> ModerateOneImageAsync(string imageUrl)
        {
            string finalUrl = imageUrl;
            if (imageUrl.Contains("localhost") || imageUrl.Contains("127.0.0.1"))
            {
                try
                {
                    var bytes = await _localHttpClient.GetByteArrayAsync(imageUrl);
                    var base64 = Convert.ToBase64String(bytes);
                    finalUrl = $"data:image/jpeg;base64,{base64}";
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[HybridScan] Lỗi khi tải ảnh local để convert sang base64: {Url}", imageUrl);
                }
            }

            var requestBody = new
            {
                model = "omni-moderation-latest",
                input = new[]
                {
                    new
                    {
                        type      = "image_url",
                        image_url = new { url = finalUrl }
                    }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await SendRequestWithRetryAsync(content);
            var responseBody = await response.Content.ReadAsStringAsync();

            var results = ParseMultipleResults(responseBody);
            return results.Count > 0
                ? results[0]
                : (new Dictionary<string, double>(), false);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  HTTP + Parsing
        // ═══════════════════════════════════════════════════════════════════

    private async Task<HttpResponseMessage> SendRequestWithRetryAsync(StringContent content)
    {
      HttpResponseMessage response;
      try
      {
        response = await _httpClient.PostAsync("https://gateway.ai.cloudflare.com/v1/37a714edab65119e71904259d5c1f144/mlndex-ai/openai/moderations", content);
      }
      catch (TaskCanceledException)
      {
        _logger.LogError("OpenAI API timeout khi kiểm duyệt ảnh");
        throw new TimeoutException("OpenAI moderation API timeout sau 120 giây");
      }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                _logger.LogError("OpenAI API lỗi {StatusCode}: {Error}", response.StatusCode, error);

                if ((int)response.StatusCode == 429)
                    throw new HttpRequestException("OpenAI rate limit — thử lại sau ít giây",
                        null, System.Net.HttpStatusCode.TooManyRequests);

                response.EnsureSuccessStatusCode();
            }

            return response;
        }

        /// <summary>
        /// Parse OpenAI moderation response — handles both single and multi-result arrays.
        /// </summary>
        private List<(Dictionary<string, double> Scores, bool Flagged)> ParseMultipleResults(string responseJson)
        {
            var allResults = new List<(Dictionary<string, double> Scores, bool Flagged)>();

            try
            {
                using var doc = JsonDocument.Parse(responseJson);
                var results = doc.RootElement.GetProperty("results");

                foreach (var result in results.EnumerateArray())
                {
                    var scores = new Dictionary<string, double>();
                    bool flagged = result.GetProperty("flagged").GetBoolean();

                    var categoryScores = result.GetProperty("category_scores");
                    foreach (var property in categoryScores.EnumerateObject())
                    {
                        scores[property.Name] = property.Value.GetDouble();
                    }

                    allResults.Add((scores, flagged));
                }
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "[HybridScan] Lỗi parse JSON từ OpenAI: {Json}",
                    responseJson.Length > 200 ? responseJson[..200] : responseJson);
            }

            return allResults;
        }

        private static string BuildFlaggedReason(Dictionary<string, double> scores)
        {
            return string.Join(", ", scores
                .Where(s => s.Value >= 0.1)
                .OrderByDescending(s => s.Value)
                .Select(s => s.Key));
        }
    }
}
