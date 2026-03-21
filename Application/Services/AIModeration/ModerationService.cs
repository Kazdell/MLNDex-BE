using Application.DTOs.AIModeration;
using Application.DTOs.Moderation;
using Application.Interfaces.AIModeration;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Application.Interfaces.Notification;
using Application.Interfaces.Queue;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Application.Services.AIModeration
{
    public class ModerationService : IModerationService
    {
        private readonly IMlndexDbContext _db;
        private readonly IAiModerationClient _aiClient;
        private readonly ILogger<ModerationService> _logger;
        private readonly IBlacklistProvider _blacklist;
        private readonly IOCRService _ocr;
        private readonly IModerationQueue _queue;
        private readonly INotificationService _notificationService;

        private static readonly Dictionary<char, char> TeencodeMap = new()
        {
            { '@', 'a' }, { '1', 'i' }, { '0', 'o' }, { '3', 'e' },
            { '!', 'i' }, { '$', 's' }, { '4', 'a' }
        };

        public ModerationService(
            IMlndexDbContext db,
            IAiModerationClient aiClient,
            ILogger<ModerationService> logger,
            IBlacklistProvider blacklist,
            IOCRService ocr,
            IModerationQueue queue,
            INotificationService notificationService)
        {
            _db = db;
            _aiClient = aiClient;
            _logger = logger;
            _blacklist = blacklist;
            _ocr = ocr;
            _queue = queue;
            _notificationService = notificationService;
        }

		private static string TruncateMessage(string msg, int maxLen = 250)
			=> msg.Length > maxLen ? msg[..(maxLen - 3)] + "..." : msg;

        // ─────────────────────────────────────────────────────────────
        // Bước 1: AI tự động kiểm duyệt khi chapter vừa upload
        // ─────────────────────────────────────────────────────────────
        public async Task<AiModerationResultDto> RunAiModerationAsync(int chapterId)
        {
            var chapter = await _db.Chapters
                .Include(c => c.Pages)
                .Include(c => c.Series) // Lấy Series để biết AgeRating
                    .ThenInclude(s => s.Creator) // Lấy Creator để biết ReputationScore
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId)
                ?? throw new KeyNotFoundException($"Không tìm thấy chapter {chapterId}");

            _logger.LogInformation("Chạy AI kiểm duyệt chapter {ChapterId} (Rating: {Rating})", chapterId, chapter.Series.AgeRating);

            // 0. Check Chapter Title Blacklist
            if (!string.IsNullOrEmpty(chapter.Title))
            {
                var titleCheck = PreCheckText(new TextCheckRequest { Text = chapter.Title, UserReputation = 100 });
                if (titleCheck.Action == ModerationActionType.AutoReject.ToString() || titleCheck.Action == ModerationActionType.InstantBan.ToString())
                {
                    chapter.ModerationStatus = ModerationStatus.REJECTED;
                    await _db.SaveChangesAsync();
                    _logger.LogWarning("Chapter {ChapterId} bị reject do tiêu đề vi phạm.", chapterId);
                    
                    await _notificationService.CreateNotificationAsync(
                        chapter.Series.Creator.UserId,
                        "Chapter bị từ chối tự động",
                        TruncateMessage($"Chương {chapter.ChapterNumber} của truyện '{chapter.Series.Title}' đã bị từ chối do tiêu đề chứa nội dung cấm."),
                        $"/creator/chapters/{chapterId}/edit",
                        Domain.Entities.NotificationType.SYSTEM
                    );
                    
                    return new AiModerationResultDto
                    {
                        Flagged = true,
                        FlaggedReason = "title_violation",
                        CategoryScores = new Dictionary<string, double>()
                    };
                }
            }

            // 1. AI Image Analysis
            var imageUrls = chapter.Pages
                .OrderBy(p => p.PageNumber)
                .Select(p => p.ImageUrl)
                .ToList();

            if (imageUrls.Count == 0)
            {
                _logger.LogWarning("Chapter {ChapterId} không có ảnh nào", chapterId);
                return new AiModerationResultDto
                {
                    Flagged = false,
                    FlaggedReason = null,
                    CategoryScores = new Dictionary<string, double>()
                };
            }

            var aiResult = await _aiClient.ModerateImagesAsync(imageUrls);

            // 2. OCR & Text Check (Dùng cho image content)
            var extractedTextBuilder = new StringBuilder();
            using var httpClient = new HttpClient(); // Khởi tạo HttpClient tạm thời cho OCR image download
            foreach (var url in imageUrls)
            {
                try
                {
                    var imageBytes = await httpClient.GetByteArrayAsync(url);
                    var text = await _ocr.ExtractTextFromImageAsync(imageBytes);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        extractedTextBuilder.AppendLine(text);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OCR lỗi ở ảnh {Url}", url);
                }
            }

            var fullText = extractedTextBuilder.ToString();
            if (!string.IsNullOrWhiteSpace(fullText))
            {
                var textResult = PreCheckText(new TextCheckRequest
                {
                    Text = fullText,
                    UserReputation = chapter.Series.Creator.ReputationScore,
                    IsComment = false
                });

                // Only auto-flag on severe violations (AutoReject/InstantBan)
                // FlagForReview (mild profanity) is recorded but doesn't force rejection
                // — the scoring engine will decide based on image scores + age rating
                if (textResult.Action == ModerationActionType.AutoReject.ToString()
                    || textResult.Action == ModerationActionType.InstantBan.ToString())
                {
                    aiResult.Flagged = true;
                }

                // Always record the OCR reasons for transparency (even if not flagged)
                if (textResult.Action != ModerationActionType.AutoPass.ToString())
                {
                    var textReason = string.Join(", ", textResult.Reasons);
                    var combinedReason = string.IsNullOrEmpty(aiResult.FlaggedReason) ? textReason : $"{aiResult.FlaggedReason} | OCR: {textReason}";
                    // Truncate to avoid excessively long strings from OCR
                    aiResult.FlaggedReason = combinedReason.Length > 200 ? combinedReason[..197] + "..." : combinedReason;
                }
            }
            // 3. Scoring Engine
            var scoreRequest = new OpenAiScoreRequest
            {
                Scores = aiResult.CategoryScores,
                TargetAgeRating = chapter.Series.AgeRating.ToString()
            };

            var analysis = AnalyzeOpenAiScores(scoreRequest);

            // 4. Quyết định hành động
            if (analysis.Action == ModerationActionType.AutoReject.ToString()
                                || analysis.Action == ModerationActionType.FlagForReview.ToString()
                                || aiResult.Flagged)
            {
                chapter.ModerationStatus = ModerationStatus.REJECTED;
                chapter.Status = ChapterStatus.DRAFT; // Rejected = not visible publicly

                var queueItem = await _db.ModerationQueues
                    .FirstOrDefaultAsync(q => q.ContentId == chapterId
                        && q.ContentType == ModerationQueueContentType.CHAPTER);

                var aiReason = aiResult.Flagged
                    ? aiResult.FlaggedReason
                    : $"{analysis.WorstCategory} (Score: {analysis.WorstScore:F2})";

                if (queueItem != null)
                {
                    // Update Priority dựa vào mức độ vi phạm
                    queueItem.Priority = analysis.Action == ModerationActionType.AutoReject.ToString()
                                          ? QueuePriority.HIGH : QueuePriority.MEDIUM;
                    queueItem.Status = QueueStatus.RESOLVED; // Mark as done
                    queueItem.FlaggedAt = DateTime.UtcNow;
                    queueItem.ReportCount += 1;

                    var report = new Report
                    {
                        ContentId = chapterId,
                        ContentType = ReportTargetType.ChapterTranslation,
                        Reason = ReportReason.Inappropriate,
                        Description = "AI_" + aiReason,
                        ReporterId = 1,
                        Queue = queueItem,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.Reports.Add(report);
                }

                _logger.LogWarning("Chapter {ChapterId} bị {Action}: {Reason}",
                    chapterId, analysis.Action, aiReason);
            }
            else
            {
                chapter.ModerationStatus = ModerationStatus.APPROVED;
                chapter.Status = ChapterStatus.PUBLISHED;
                chapter.PublishedAt = DateTime.UtcNow;

                // Mark queue as resolved for approved chapters too
                var approvedQueueItem = await _db.ModerationQueues
                    .FirstOrDefaultAsync(q => q.ContentId == chapterId
                        && q.ContentType == ModerationQueueContentType.CHAPTER);
                if (approvedQueueItem != null)
                    approvedQueueItem.Status = QueueStatus.RESOLVED;

                _logger.LogInformation("Chapter {ChapterId} đã được AI tự động duyệt → PUBLISHED", chapterId);
            }

            // Save both aggregate scores and per-page results as structured JSON
            var scoresData = new Dictionary<string, object>
            {
                ["categoryScores"] = aiResult.CategoryScores,
                ["scanMode"] = aiResult.ScanMode ?? "unknown",
            };
            if (aiResult.PerPageResults?.Count > 0)
                scoresData["perPageResults"] = aiResult.PerPageResults;
            chapter.AiScoresJson = JsonSerializer.Serialize(scoresData);

            await _db.SaveChangesAsync();
            return aiResult;
        }

        public async Task<AiModerationResultDto> RunSeriesModerationAsync(int seriesId)
        {
            var series = await _db.Series
                .Include(s => s.Creator)
                .FirstOrDefaultAsync(s => s.SeriesId == seriesId)
                ?? throw new KeyNotFoundException($"Không tìm thấy series {seriesId}");

            _logger.LogInformation("Chạy AI kiểm duyệt Series {SeriesId} (Rating: {Rating})", seriesId, series.AgeRating);

            // 0. Check Series Title Blacklist
            if (!string.IsNullOrEmpty(series.Title))
            {
                var titleCheck = PreCheckText(new TextCheckRequest { Text = series.Title, UserReputation = 100 });
                if (titleCheck.Action == ModerationActionType.AutoReject.ToString() ||
                    titleCheck.Action == ModerationActionType.InstantBan.ToString())
                {
                    series.ModerationStatus = ModerationStatus.REJECTED;
                    await _db.SaveChangesAsync();
                    _logger.LogWarning("Series {SeriesId} bị reject do tiêu đề vi phạm.", seriesId);
                    return new AiModerationResultDto
                    {
                        Flagged = true,
                        FlaggedReason = "title_violation",
                        CategoryScores = new Dictionary<string, double>()
                    };
                }
            }

            // 1. Check Description Blacklist
            if (!string.IsNullOrEmpty(series.Description))
            {
                var descCheck = PreCheckText(new TextCheckRequest { Text = series.Description, UserReputation = 100 });
                if (descCheck.Action == ModerationActionType.AutoReject.ToString() ||
                    descCheck.Action == ModerationActionType.InstantBan.ToString())
                {
                    series.ModerationStatus = ModerationStatus.REJECTED;
                    await _db.SaveChangesAsync();
                    _logger.LogWarning("Series {SeriesId} bị reject do mô tả vi phạm.", seriesId);
                    return new AiModerationResultDto
                    {
                        Flagged = true,
                        FlaggedReason = "description_violation",
                        CategoryScores = new Dictionary<string, double>()
                    };
                }
            }

            // 2. AI Cover Image Analysis
            if (string.IsNullOrEmpty(series.CoverImageUrl))
            {
                _logger.LogWarning("Series {SeriesId} không có ảnh bìa", seriesId);
                series.ModerationStatus = ModerationStatus.APPROVED;
                await _db.SaveChangesAsync();
                return new AiModerationResultDto
                {
                    Flagged = false,
                    FlaggedReason = null,
                    CategoryScores = new Dictionary<string, double>()
                };
            }

            var aiResult = await _aiClient.ModerateImagesAsync(new[] { series.CoverImageUrl });

            // 3. Scoring Engine
            var scoreRequest = new OpenAiScoreRequest
            {
                Scores = aiResult.CategoryScores,
                TargetAgeRating = series.AgeRating.ToString()
            };

            var analysis = AnalyzeOpenAiScores(scoreRequest);

            // 4. Quyết định hành động
            if (analysis.Action == ModerationActionType.AutoReject.ToString() ||
                analysis.Action == ModerationActionType.FlagForReview.ToString() ||
                aiResult.Flagged)
            {
                series.ModerationStatus = ModerationStatus.PENDING;

                var queueItem = await _db.ModerationQueues
                    .FirstOrDefaultAsync(q => q.ContentId == seriesId && q.ContentType == ModerationQueueContentType.SERIES);

                if (queueItem == null)
                {
                    queueItem = new ModerationQueue
                    {
                        ContentId = seriesId,
                        ContentType = ModerationQueueContentType.SERIES,
                        Priority = (analysis.Action == ModerationActionType.AutoReject.ToString())
                            ? QueuePriority.HIGH
                            : QueuePriority.MEDIUM,
                        Status = QueueStatus.PENDING,
                        FlaggedAt = DateTime.UtcNow,
                        ReportCount = 0,
                        AppealCount = 0
                    };
                    _db.ModerationQueues.Add(queueItem);
                }
                else
                {
                    queueItem.Priority = (analysis.Action == ModerationActionType.AutoReject.ToString())
                        ? QueuePriority.HIGH
                        : QueuePriority.MEDIUM;
                    queueItem.Status = QueueStatus.PENDING;
                    queueItem.FlaggedAt = DateTime.UtcNow;
                }

                var aiReason = aiResult.Flagged
                    ? aiResult.FlaggedReason
                    : $"{analysis.WorstCategory} (Score: {analysis.WorstScore:F2})";

                var report = new Report
                {
                    ContentId = seriesId,
                    ContentType = ReportTargetType.Series,
                    Reason = ReportReason.Inappropriate,
                    Description = "AI_" + aiReason,
                    ReporterId = 1,
                    Queue = queueItem,
                    CreatedAt = DateTime.UtcNow
                };

                _db.Reports.Add(report);
                queueItem.ReportCount += 1;

                _logger.LogWarning("Series {SeriesId} bị {Action}: {Reason}", seriesId, analysis.Action, aiReason);
            }
            else
            {
                series.ModerationStatus = ModerationStatus.APPROVED;
                _logger.LogInformation("Series {SeriesId} đã được AI tự động duyệt", seriesId);
            }

            await _db.SaveChangesAsync();
            return aiResult;
        }

        // ─────────────────────────────────────────────────────────────
        // Bước 2: Tác giả appeal → tạo queue cho moderator xử lý
        // ─────────────────────────────────────────────────────────────
        public async Task SubmitAppealAsync(int chapterId, int requestedByUserId, string appealReason)
        {
            var chapter = await _db.Chapters
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId)
                ?? throw new KeyNotFoundException($"Không tìm thấy chapter {chapterId}");

            // Chỉ cho appeal khi đang bị Flagged
            if (chapter.ModerationStatus != ModerationStatus.REJECTED)
                throw new InvalidOperationException("Chỉ có thể appeal khi chapter đang bị reject.");

            // Đếm số lần đã appeal trước đó
            var appealCount = await _db.ModerationQueues
                .CountAsync(q => q.ContentId == chapterId
                              && q.ContentType == ModerationQueueContentType.CHAPTER);

            // Tìm queue hiện tại hoặc tạo mới
            var queue = await _db.ModerationQueues.FirstOrDefaultAsync(q => q.ContentId == chapterId && q.ContentType == ModerationQueueContentType.CHAPTER);
            if (queue == null)
            {
                queue = new ModerationQueue
                {
                    ContentId = chapterId,
                    ContentType = ModerationQueueContentType.CHAPTER,
                    Priority = QueuePriority.MEDIUM,
                    Status = QueueStatus.PENDING,
                    ReportCount = 0,
                    FlaggedAt = DateTime.UtcNow
                };
                _db.ModerationQueues.Add(queue);
            }

            queue.Status = QueueStatus.PENDING;
            queue.AppealReason = appealReason;
            queue.AppealCount = appealCount + 1;
            queue.FlaggedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Tác giả {UserId} appeal chapter {ChapterId} lần {Count}",
                requestedByUserId, chapterId, appealCount + 1);
        }

        // ─────────────────────────────────────────────────────────────
        // Private helpers
        // ─────────────────────────────────────────────────────────────

        private async Task<string?> GetLastAiFlaggedReasonAsync(int chapterId)
        {
            return await _db.Reports
                .Where(r => r.ContentId == chapterId
                         && r.ContentType == ReportTargetType.ChapterTranslation
                         && r.Description != null && r.Description.StartsWith("AI_"))
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.Description)
                .FirstOrDefaultAsync();
        }

        // Pre-check text against blacklist with teencode normalization.
        public TextCheckResponse PreCheckText(TextCheckRequest request)
        {
            var cleanedText = CleanText(request.Text);
            int penaltyScore = 0;
            var flagReasons = new List<string>();

            // Illegal content -> Instant ban
            foreach (var entry in _blacklist.IllegalContentList)
            {
                if (ContainsWord(cleanedText, entry))
                {
                    return new TextCheckResponse
                    {
                        Action = ModerationActionType.InstantBan.ToString(),
                        Reasons = new List<string> { $"Illegal content: {entry.Word}" },
                        PenaltyPoints = 100,
                        TemplateId = "REJ_005",
                        IsPermaBan = true
                    };
                }
            }

            // Hate speech -> Auto reject if extreme
            foreach (var entry in _blacklist.HateSpeechList)
            {
                if (ContainsWord(cleanedText, entry))
                {
                    int points = SeverityToPoints(entry.Severity);
                    if (entry.Severity == "extreme")
                    {
                        return new TextCheckResponse
                        {
                            Action = ModerationActionType.AutoReject.ToString(),
                            Reasons = new List<string> { $"Hate speech: {entry.Word}" },
                            PenaltyPoints = points,
                            TemplateId = "REJ_008",
                            IsPermaBan = false
                        };
                    }
                    penaltyScore += points;
                    flagReasons.Add($"Hate speech: {entry.Word}");
                }
            }

            // Profanity -> Accumulate penalty
            foreach (var entry in _blacklist.ProfanityList)
            {
                if (ContainsWord(cleanedText, entry))
                {
                    penaltyScore += SeverityToPoints(entry.Severity);
                    flagReasons.Add($"Profanity: {entry.Word}");
                }
            }

            // Low-rep users get harsher penalties
            if (request.UserReputation < 50 && !request.IsComment)
                penaltyScore = (int)(penaltyScore * 1.5);

            int threshold = request.IsComment ? 30 : 15;

            if (penaltyScore >= threshold)
            {
                return new TextCheckResponse
                {
                    Action = ModerationActionType.FlagForReview.ToString(),
                    Reasons = flagReasons,
                    PenaltyPoints = penaltyScore,
                    TemplateId = "REJ_010"
                };
            }

            return new TextCheckResponse
            {
                Action = ModerationActionType.AutoPass.ToString(),
                Reasons = new List<string>(),
                PenaltyPoints = penaltyScore
            };
        }

        // Analyze OpenAI scores against thresholds and target rating.
        public OpenAiScoreResponse AnalyzeOpenAiScores(OpenAiScoreRequest request)
        {
            // 1. Kiểm soát CSAM (Zero Tolerance)
            if (request.Scores.TryGetValue("sexual/minors", out double csamScore) && csamScore >= 0.2)
            {
                return new OpenAiScoreResponse
                {
                    Action = ModerationActionType.InstantBan.ToString(),
                    WorstCategory = "sexual/minors",
                    WorstScore = csamScore,
                    TemplateId = "REJ_005",
                    IsPermaBan = true,
                    ReputationDeduction = 100
                };
            }

            string? worstCategory = null;
            double worstScore = 0;
            ModerationActionType worstAction = ModerationActionType.AutoPass;

            // 2. Chế độ Aging-based Scoring (Yêu cầu của Sếp)
            // Lấy max_allowed_score từ config cho rating được chọn
            double maxAllowed = 0.5; // Fallback
            switch (request.TargetAgeRating.ToUpper())
            {
                case "ALL": maxAllowed = 0.1; break;
                case "TEEN": maxAllowed = 0.35; break;
                case "MATURE": maxAllowed = 0.6; break;
                case "ADULT": maxAllowed = 0.89; break;
            }

            foreach (var kvp in request.Scores)
            {
                if (!_blacklist.Thresholds.TryGetValue(kvp.Key, out var rule)) continue;

                ModerationActionType currentAction = ModerationActionType.AutoPass;

                // Nếu score vượt quá ngưỡng của Rating đã chọn -> Flag hoặc Reject
                if (kvp.Value >= rule.AUTO_REJECT)
                {
                    currentAction = ModerationActionType.AutoReject;
                }
                else if (kvp.Value >= maxAllowed || kvp.Value >= rule.FLAG_FOR_REVIEW)
                {
                    currentAction = ModerationActionType.FlagForReview;
                }

                if (currentAction > worstAction || (currentAction == worstAction && kvp.Value > worstScore))
                {
                    worstAction = currentAction;
                    worstCategory = kvp.Key;
                    worstScore = kvp.Value;
                }
            }

            var response = new OpenAiScoreResponse
            {
                Action = worstAction.ToString(),
                WorstCategory = worstCategory,
                WorstScore = worstScore,
                ReputationDeduction = worstAction == ModerationActionType.AutoReject ? 30 : 0
            };

            if (worstAction == ModerationActionType.AutoReject && worstCategory != null)
                response.TemplateId = GetTemplateForCategory(worstCategory);

            if (worstAction == ModerationActionType.AutoPass || worstAction == ModerationActionType.FlagForReview)
                response.SuggestedAgeRating = AssignAgeRating(request.Scores).ToString();

            return response;
        }

        public List<RejectionTemplateDto> GetRejectionTemplates() => _blacklist.RejectionTemplates;

        public List<BannedTagDto> GetBannedTags()
        {
            var all = new List<BannedTagDto>();
            all.AddRange(_blacklist.BannedTags);
            all.AddRange(_blacklist.RestrictedTags);
            return all;
        }

        // --- Private Helpers ---

        // Normalize teencode and collapse obfuscation spacing.
        private static string CleanText(string raw)
        {
            var normalized = raw.ToLower();

            var chars = normalized.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (TeencodeMap.TryGetValue(chars[i], out char replacement))
                    chars[i] = replacement;
            }
            normalized = new string(chars);

            var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var result = new List<string>();
            var singleCharBuffer = new List<string>();

            foreach (var part in parts)
            {
                if (part.Length <= 2)
                {
                    singleCharBuffer.Add(part);
                }
                else
                {
                    if (singleCharBuffer.Count > 1)
                        result.Add(string.Join("", singleCharBuffer));
                    else if (singleCharBuffer.Count == 1)
                        result.Add(singleCharBuffer[0]);
                    singleCharBuffer.Clear();
                    result.Add(part);
                }
            }

            if (singleCharBuffer.Count > 1)
                result.Add(string.Join("", singleCharBuffer));
            else if (singleCharBuffer.Count == 1)
                result.Add(singleCharBuffer[0]);

            return string.Join(" ", result);
        }

        // Cache compiled regexes for short words to avoid repeated compilation
        private static readonly global::System.Collections.Concurrent.ConcurrentDictionary<string, Regex> _wordBoundaryCache = new();

        private static bool ContainsWord(string text, BlacklistEntry entry)
        {
            if (MatchesWord(text, entry.Word))
                return true;

            foreach (var variant in entry.Variants)
            {
                if (MatchesWord(text, variant))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// For short words (< 4 chars), use word-boundary regex to avoid false positives
        /// from OCR text (e.g. "document" matching "cu", "admin" matching "dm").
        /// For longer words, substring match is sufficient and faster.
        /// </summary>
        private static bool MatchesWord(string text, string word)
        {
            if (string.IsNullOrEmpty(word)) return false;

            // Long words: substring match is safe enough (low false positive risk)
            if (word.Length >= 4)
                return text.Contains(word, StringComparison.OrdinalIgnoreCase);

            // Short words: require word boundaries to prevent "document" matching "cu"
            var regex = _wordBoundaryCache.GetOrAdd(word.ToLowerInvariant(), w =>
                new Regex($@"\b{Regex.Escape(w)}\b", RegexOptions.IgnoreCase | RegexOptions.Compiled));
            return regex.IsMatch(text);
        }

        private static int SeverityToPoints(string severity) => severity.ToLower() switch
        {
            "low" => 3,
            "medium" => 8,
            "high" => 15,
            "extreme" => 50,
            _ => 5
        };

        private static string GetTemplateForCategory(string category) => category switch
        {
            "violence" => "REJ_001",
            "sexual" => "REJ_004",
            "sexual/minors" => "REJ_005",
            "hate" or "hate/threatening" => "REJ_007",
            "self-harm" => "REJ_002",
            "harassment" => "REJ_009",
            _ => "REJ_001"
        };

        private static AgeRating AssignAgeRating(Dictionary<string, double> scores)
        {
            double maxViolence = scores.GetValueOrDefault("violence", 0);
            double maxSexual = scores.GetValueOrDefault("sexual", 0);
            double maxHate = scores.GetValueOrDefault("hate", 0);
            double overallMax = Math.Max(maxViolence, Math.Max(maxSexual, maxHate));

            if (overallMax >= 0.6) return AgeRating.ADULT;
            if (overallMax >= 0.4) return AgeRating.MATURE;
            if (overallMax >= 0.2) return AgeRating.TEEN;
            return AgeRating.ALL;
        }

        public async Task<AiModerationResultDto?> GetResultAsync(
    int chapterId, CancellationToken ct = default)
        {
            var chapter = await _db.Chapters
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct);

            if (chapter == null) return null;

            // Deserialize scores — backward-compatible with old flat dict format
            var categoryScores = new Dictionary<string, double>();
            List<PageModerationDto>? perPageResults = null;

            if (!string.IsNullOrEmpty(chapter.AiScoresJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(chapter.AiScoresJson);

                    // Format 1 (new): { categoryScores: {...}, perPageResults: [...] }
                    if (doc.RootElement.TryGetProperty("categoryScores", out var scoresEl))
                    {
                        categoryScores = JsonSerializer.Deserialize<Dictionary<string, double>>(scoresEl.GetRawText())
                                         ?? new Dictionary<string, double>();

                        if (doc.RootElement.TryGetProperty("perPageResults", out var pagesEl))
                        {
                            perPageResults = JsonSerializer.Deserialize<List<PageModerationDto>>(pagesEl.GetRawText());
                        }
                    }
                    else
                    {
                        // Format 2 (old/hybrid): flat dict possibly mixed with perPageResults at root
                        // Iterate properties: numbers → categoryScores, array → perPageResults
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Number)
                            {
                                categoryScores[prop.Name] = prop.Value.GetDouble();
                            }
                            else if (prop.Name == "perPageResults" && prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                perPageResults = JsonSerializer.Deserialize<List<PageModerationDto>>(prop.Value.GetRawText());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize AiScoresJson for chapter {ChapterId}", chapterId);
                }
            }

            // Filter out foreign_profanity_* sub-categories (false positives on image-only content)
            categoryScores = categoryScores
                .Where(kvp => !kvp.Key.StartsWith("foreign_profanity"))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            // Chapter APPROVED — không vi phạm
            if (chapter.ModerationStatus == ModerationStatus.APPROVED)
                return new AiModerationResultDto
                {
                    Flagged = false,
                    FlaggedReason = null,
                    CategoryScores = categoryScores,
                    PerPageResults = perPageResults
                };

            // Chapter PENDING/REJECTED — lấy flagged reason từ Report
            var aiReport = await _db.Reports
                .Where(r => r.ContentId == chapterId
                         && r.ContentType == ReportTargetType.ChapterTranslation
                         && r.Description != null
                         && r.Description.StartsWith("AI_"))
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);

            string? flaggedReason = null;
            if (aiReport?.Description != null)
            {
                flaggedReason = aiReport.Description.Replace("AI_", "").Split(" (Score:")[0].Trim();
            }

            return new AiModerationResultDto
            {
                Flagged = true,
                FlaggedReason = flaggedReason,
                CategoryScores = categoryScores,
                PerPageResults = perPageResults
            };
        }

        // ─────────────────────────────────────────────────────────────
        // Enqueue: tạo DB ModerationQueue record + gửi signal Channel
        // ─────────────────────────────────────────────────────────────

        public async Task EnqueueChapterForModerationAsync(int chapterId, CancellationToken ct = default)
        {
            // Clean up ALL existing queue entries for this chapter
            // to prevent duplicate processing and duplicate notifications
            var oldEntries = await _db.ModerationQueues
                .Where(q => q.ContentId == chapterId
                         && q.ContentType == ModerationQueueContentType.CHAPTER)
                .ToListAsync(ct);
            if (oldEntries.Count > 0)
            {
                // Delete linked Reports first (FK constraint: Report.QueueId → ModerationQueue)
                var queueIds = oldEntries.Select(q => q.QueueId).ToList();
                var linkedReports = await _db.Reports
                    .Where(r => r.QueueId.HasValue && queueIds.Contains(r.QueueId.Value))
                    .ToListAsync(ct);
                if (linkedReports.Count > 0)
                    _db.Reports.RemoveRange(linkedReports);

                // Delete linked ModerationActions (FK constraint: ModerationAction.QueueId → ModerationQueue)
                var linkedActions = await _db.ModerationActions
                    .Where(a => queueIds.Contains(a.QueueId))
                    .ToListAsync(ct);
                if (linkedActions.Count > 0)
                    _db.ModerationActions.RemoveRange(linkedActions);

                _db.ModerationQueues.RemoveRange(oldEntries);
                await _db.SaveChangesAsync(ct);
            }

            _db.ModerationQueues.Add(new ModerationQueue
            {
                ContentId = chapterId,
                ContentType = ModerationQueueContentType.CHAPTER,
                Priority = QueuePriority.HIGH,
                Status = QueueStatus.PENDING,
                FlaggedAt = DateTime.UtcNow,
                ReportCount = 0,
                AppealCount = 0,
            });
            await _db.SaveChangesAsync(ct);
            await _queue.EnqueueAsync(chapterId, ct);

            // Send "queued" notification to creator
            var chapter = await _db.Chapters
                .Include(c => c.Series)
                    .ThenInclude(s => s.Creator)
                .FirstOrDefaultAsync(c => c.ChapterId == chapterId, ct);

            if (chapter?.Series?.Creator != null)
            {
                var creatorId = chapter.Series.Creator.UserId;
                await _notificationService.CreateNotificationAsync(
                    creatorId,
                    "Hàng chờ kiểm duyệt",
                    $"Chương {chapter.ChapterNumber} của \"{chapter.Series.Title}\" đã vào hàng chờ kiểm duyệt AI",
                    $"/creator/moderation-result?chapterId={chapterId}",
                    NotificationType.SYSTEM
                );
            }

            _logger.LogInformation(
                "[ModerationService] ChapterId={ChapterId} đã vào moderation queue.", chapterId);
        }

        public async Task EnqueueSeriesForModerationAsync(int seriesId, CancellationToken ct = default)
        {
            _db.ModerationQueues.Add(new ModerationQueue
            {
                ContentId = seriesId,
                ContentType = ModerationQueueContentType.SERIES,
                Priority = QueuePriority.HIGH,
                Status = QueueStatus.PENDING,
                FlaggedAt = DateTime.UtcNow,
                ReportCount = 0,
                AppealCount = 0,
            });
            await _db.SaveChangesAsync(ct);
            await _queue.EnqueueAsync(seriesId, ct);

            _logger.LogInformation(
                "[ModerationService] SeriesId={SeriesId} đã vào moderation queue.", seriesId);
        }
    }
}
