using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Application.Interfaces.Translation;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Adapters.Translation
{
    public class GoogleTranslationClient : IGoogleTranslationClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GoogleTranslationClient> _logger;

        public GoogleTranslationClient(HttpClient httpClient, ILogger<GoogleTranslationClient> logger)
        {
            _httpClient = httpClient;
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Add a proper User-Agent to prevent Google from immediately blocking the request, especially over VPNs.
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            }
            
            _logger = logger;
        }

        public async Task<List<string>> TranslateTextsAsync(List<string> texts, string sourceLang = "auto", string targetLang = "vi")
        {
            if (texts == null || !texts.Any()) return new List<string>();

            // Convert frontend names or Tesseract codes to Google 2-letter codes
            string MapLanguageToCode(string lang)
            {
                if (string.IsNullOrEmpty(lang)) return "auto";
                lang = lang.ToLower();
                if (lang.Contains("jap") || lang == "jpn" || lang == "ja") return "ja";
                if (lang.Contains("viet") || lang == "vie" || lang == "vi") return "vi";
                if (lang.Contains("eng") || lang == "en") return "en";
                if (lang.Contains("kor") || lang == "ko") return "ko";
                if (lang.Contains("chi") || lang == "zh") return "zh-CN";
                if (lang.Contains("fra") || lang == "fr") return "fr";
                if (lang.Contains("spa") || lang == "es") return "es";
                return "auto";
            }

            string sl = MapLanguageToCode(sourceLang);
            string tl = MapLanguageToCode(targetLang);
            if (tl == "auto") tl = "vi"; // Target language must be concrete
            var results = new string[texts.Count];
            var delimiter = "\n\n[===]\n\n";

            var batches = new List<List<int>>();
            var currentBatch = new List<int>();
            int currentLength = 0;

            for (int i = 0; i < texts.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(texts[i]))
                {
                    results[i] = "";
                    continue;
                }

                var textLen = texts[i].Length;
                if (currentLength + textLen + delimiter.Length > 1500 && currentBatch.Count > 0)
                {
                    batches.Add(new List<int>(currentBatch));
                    currentBatch.Clear();
                    currentLength = 0;
                }

                currentBatch.Add(i);
                currentLength += textLen + delimiter.Length;
            }

            if (currentBatch.Count > 0)
            {
                batches.Add(currentBatch);
            }

            for (int b = 0; b < batches.Count; b++)
            {
                var indices = batches[b];
                var batchTexts = indices.Select(idx => (texts[idx] ?? "").Replace("[===]", "").Trim()).ToList();
                var joinedText = string.Join(delimiter, batchTexts);

                if (b > 0)
                {
                    await Task.Delay(1000); // Wait 1s between batches
                }

                int maxRetries = 3;
                bool success = false;

                for (int retry = 0; retry < maxRetries; retry++)
                {
                    try
                    {
                        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={tl}&dt=t&q={Uri.EscapeDataString(joinedText)}";
                        _logger.LogInformation("Translate Batch {Batch}/{Total}: sl={sl}, tl={tl}", b + 1, batches.Count, sl, tl);

                        var response = await _httpClient.GetAsync(url);
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(content);
                            var rootArray = doc.RootElement;
                            
                            string fullTranslation = "";
                            if (rootArray.ValueKind == JsonValueKind.Array && rootArray.GetArrayLength() > 0)
                            {
                                var textParts = rootArray[0];
                                foreach (var part in textParts.EnumerateArray())
                                {
                                    if (part.ValueKind == JsonValueKind.Array && part.GetArrayLength() > 0)
                                    {
                                        fullTranslation += part[0].GetString() ?? "";
                                    }
                                }
                            }

                            // Splitting the translated full string using Regex to handle variations of [===]
                            // Google Translate sometimes adds spaces inside brackets e.g. [ === ]
                            var translatedParts = System.Text.RegularExpressions.Regex.Split(
                                fullTranslation, 
                                @"\[\s*={1,5}\s*\]"
                            );

                            // Match the parsed sections back to their original indices
                            for (int i = 0; i < indices.Count; i++)
                            {
                                int originalIdx = indices[i];
                                if (i < translatedParts.Length)
                                {
                                    results[originalIdx] = translatedParts[i].Trim();
                                }
                                else
                                {
                                    results[originalIdx] = texts[originalIdx]; // Fallback if splitted incorrectly
                                }
                            }

                            success = true;
                            break; // Done with this batch inside retry loop
                        }
                        else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                        {
                            _logger.LogWarning("Google API 429 TooManyRequests for batch {Batch}. Retry {Retry}/{Max}", b, retry + 1, maxRetries);
                            await Task.Delay(1500 * (retry + 1));
                        }
                        else
                        {
                            _logger.LogWarning("Google API failed: {StatusCode} for batch {Batch}", response.StatusCode, b);
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception in translation batch {Batch}", b);
                        if (retry == maxRetries - 1) break;
                        await Task.Delay(1000);
                    }
                }

                if (!success)
                {
                    // Fallback to original text if batch failed permanently
                    foreach (var idx in indices)
                    {
                        results[idx] = texts[idx];
                    }
                }
            }

            return results.ToList();
        }
    }
}
