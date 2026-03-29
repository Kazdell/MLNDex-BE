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
            _logger = logger;
        }

        public async Task<List<string>> TranslateTextsAsync(List<string> texts, string sourceLang = "auto", string targetLang = "vi")
        {
            if (texts == null || !texts.Any()) return new List<string>();

            // Convert to Tesseract language codes to Google codes if needed
            string sl = sourceLang == "eng" ? "en" : sourceLang == "jpn" ? "ja" : sourceLang == "kor" ? "ko" : sourceLang == "chi_sim" ? "zh-CN" : "auto";
            
            var results = new string[texts.Count];
            var tasks = new List<Task>();
            
            // To prevent massive concurrent spamming to Google that could cause 429, we batch requests
            var semaphore = new System.Threading.SemaphoreSlim(5); // 5 concurrent requests

            for (int i = 0; i < texts.Count; i++)
            {
                var index = i;
                var originalText = texts[i];

                tasks.Add(Task.Run(async () =>
                {
                    if (string.IsNullOrWhiteSpace(originalText))
                    {
                        results[index] = "";
                        return;
                    }

                    await semaphore.WaitAsync();
                    try
                    {
                        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={targetLang}&dt=t&q={Uri.EscapeDataString(originalText)}";
                        var response = await _httpClient.GetAsync(url);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            var content = await response.Content.ReadAsStringAsync();
                            // Google's format: [[["translated", "original", null, null, 1]], null, "en"]
                            using var doc = JsonDocument.Parse(content);
                            var rootArray = doc.RootElement;
                            if (rootArray.ValueKind == JsonValueKind.Array && rootArray.GetArrayLength() > 0)
                            {
                                var textParts = rootArray[0];
                                string fullTranslation = "";
                                foreach (var part in textParts.EnumerateArray())
                                {
                                    if (part.ValueKind == JsonValueKind.Array && part.GetArrayLength() > 0)
                                    {
                                        fullTranslation += part[0].GetString() ?? "";
                                    }
                                }
                                results[index] = fullTranslation;
                            }
                            else
                            {
                                results[index] = originalText;
                            }
                        }
                        else
                        {
                            _logger.LogWarning("Google Translation API returned {StatusCode} for text index {Index}", response.StatusCode, index);
                            results[index] = originalText;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to translate text index {Index}", index);
                        results[index] = originalText; // fallback
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return results.ToList();
        }
    }
}
