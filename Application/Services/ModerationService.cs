using Application.DTOs.Moderation;
using Application.Interfaces;
using Domain.Enums;

namespace Application.Services
{
    /// <summary>
    /// Core moderation engine implementing Person #3's Content Policy rules.
    /// Handles: text pre-check (blacklist), OpenAI score analysis, age rating assignment.
    /// Designed to be Mock-ready: works independently without OCR/OpenAI/Queue modules.
    /// </summary>
    public class ModerationService : IModerationService
    {
        private readonly BlacklistProvider _blacklist;

        // Teencode mapping for Vietnamese text normalization
        private static readonly Dictionary<char, char> TeencodeMap = new()
        {
            { '@', 'a' }, { '1', 'i' }, { '0', 'o' }, { '3', 'e' },
            { '!', 'i' }, { '$', 's' }, { '4', 'a' }
        };

        public ModerationService(BlacklistProvider blacklist)
        {
            _blacklist = blacklist;
        }

        #region Text Pre-Check (Blacklist/Teencode)

        /// <inheritdoc />
        public TextCheckResponse PreCheckText(TextCheckRequest request)
        {
            var cleanedText = CleanText(request.Text);
            int penaltyScore = 0;
            var flagReasons = new List<string>();

            // Check illegal content first (CSAM, drugs, gambling) -> Instant action
            foreach (var entry in _blacklist.IllegalContentList)
            {
                if (ContainsWord(cleanedText, entry))
                {
                    return new TextCheckResponse
                    {
                        Action = ModerationAction.InstantBan.ToString(),
                        Reasons = new List<string> { $"Illegal content: {entry.Word}" },
                        PenaltyPoints = 100,
                        TemplateId = "REJ_005",
                        IsPermaBan = true
                    };
                }
            }

            // Check hate speech -> Auto reject
            foreach (var entry in _blacklist.HateSpeechList)
            {
                if (ContainsWord(cleanedText, entry))
                {
                    int points = SeverityToPoints(entry.Severity);
                    if (entry.Severity == "extreme")
                    {
                        return new TextCheckResponse
                        {
                            Action = ModerationAction.AutoReject.ToString(),
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

            // Check profanity -> Accumulate penalty
            foreach (var entry in _blacklist.ProfanityList)
            {
                if (ContainsWord(cleanedText, entry))
                {
                    penaltyScore += SeverityToPoints(entry.Severity);
                    flagReasons.Add($"Profanity: {entry.Word}");
                }
            }

            // Apply reputation multiplier: low-rep users get harsher penalties
            if (request.UserReputation < 50 && !request.IsComment)
            {
                penaltyScore = (int)(penaltyScore * 1.5);
            }

            // Threshold: comments are more lenient than story content
            int threshold = request.IsComment ? 30 : 15;

            if (penaltyScore >= threshold)
            {
                return new TextCheckResponse
                {
                    Action = ModerationAction.FlagForReview.ToString(),
                    Reasons = flagReasons,
                    PenaltyPoints = penaltyScore,
                    TemplateId = "REJ_010"
                };
            }

            return new TextCheckResponse
            {
                Action = ModerationAction.AutoPass.ToString(),
                Reasons = new List<string>(),
                PenaltyPoints = penaltyScore
            };
        }

        #endregion

        #region OpenAI Score Analysis

        /// <inheritdoc />
        public OpenAiScoreResponse AnalyzeOpenAiScores(OpenAiScoreRequest request)
        {
            // Zero-tolerance check for CSAM
            if (request.Scores.TryGetValue("sexual/minors", out double csamScore) && csamScore >= 0.3)
            {
                return new OpenAiScoreResponse
                {
                    Action = ModerationAction.InstantBan.ToString(),
                    WorstCategory = "sexual/minors",
                    WorstScore = csamScore,
                    TemplateId = "REJ_005",
                    IsPermaBan = true,
                    ReputationDeduction = 100
                };
            }

            // Worst-score-wins logic
            string? worstCategory = null;
            double worstScore = 0;
            ModerationAction worstAction = ModerationAction.AutoPass;

            foreach (var kvp in request.Scores)
            {
                if (!_blacklist.Thresholds.TryGetValue(kvp.Key, out var rule)) continue;

                ModerationAction currentAction;
                if (kvp.Value >= rule.AUTO_REJECT)
                    currentAction = ModerationAction.AutoReject;
                else if (kvp.Value >= rule.FLAG_FOR_REVIEW)
                    currentAction = ModerationAction.FlagForReview;
                else
                    currentAction = ModerationAction.AutoPass;

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
                ReputationDeduction = worstAction == ModerationAction.AutoReject ? 30 : 0
            };

            // Assign template based on worst category
            if (worstAction == ModerationAction.AutoReject && worstCategory != null)
            {
                response.TemplateId = GetTemplateForCategory(worstCategory);
            }

            // Assign age rating if content passes
            if (worstAction == ModerationAction.AutoPass || worstAction == ModerationAction.FlagForReview)
            {
                response.SuggestedAgeRating = AssignAgeRating(request.Scores).ToString();
            }

            return response;
        }

        #endregion

        #region Templates & Tags

        /// <inheritdoc />
        public List<RejectionTemplateDto> GetRejectionTemplates()
        {
            return _blacklist.RejectionTemplates;
        }

        /// <inheritdoc />
        public List<BannedTagDto> GetBannedTags()
        {
            var all = new List<BannedTagDto>();
            all.AddRange(_blacklist.BannedTags);
            all.AddRange(_blacklist.RestrictedTags);
            return all;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Clean text by normalizing teencode and removing obfuscation spaces.
        /// E.g., "v c l" -> "vcl", "đ!t" -> "đit"
        /// </summary>
        private static string CleanText(string raw)
        {
            var normalized = raw.ToLower();

            // Step 1: Map teencode characters
            var chars = normalized.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (TeencodeMap.TryGetValue(chars[i], out char replacement))
                {
                    chars[i] = replacement;
                }
            }
            normalized = new string(chars);

            // Step 2: Collapse suspicious single-char spacing (e.g. "v c l" -> "vcl")
            // Only collapse when we see single chars separated by spaces
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
                    {
                        result.Add(string.Join("", singleCharBuffer));
                    }
                    else if (singleCharBuffer.Count == 1)
                    {
                        result.Add(singleCharBuffer[0]);
                    }
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

        /// <summary>
        /// Check if cleaned text contains any variant of a blacklist entry.
        /// </summary>
        private static bool ContainsWord(string text, BlacklistEntry entry)
        {
            // Check the main word
            if (text.Contains(entry.Word, StringComparison.OrdinalIgnoreCase))
                return true;

            // Check all variants
            foreach (var variant in entry.Variants)
            {
                if (text.Contains(variant, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Convert severity string to penalty points.
        /// </summary>
        private static int SeverityToPoints(string severity) => severity.ToLower() switch
        {
            "low" => 3,
            "medium" => 8,
            "high" => 15,
            "extreme" => 50,
            _ => 5
        };

        /// <summary>
        /// Get rejection template ID based on OpenAI category.
        /// </summary>
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

        /// <summary>
        /// Assign age rating based on the highest OpenAI scores.
        /// </summary>
        private static AgeRating AssignAgeRating(Dictionary<string, double> scores)
        {
            double maxViolence = scores.GetValueOrDefault("violence", 0);
            double maxSexual = scores.GetValueOrDefault("sexual", 0);
            double maxHate = scores.GetValueOrDefault("hate", 0);

            double overallMax = Math.Max(maxViolence, Math.Max(maxSexual, maxHate));

            if (overallMax >= 0.6) return AgeRating.Adult18;
            if (overallMax >= 0.4) return AgeRating.Mature16;
            if (overallMax >= 0.2) return AgeRating.Teen13;
            return AgeRating.AllAges;
        }

        #endregion
    }
}
