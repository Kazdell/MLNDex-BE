using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Domain.Entities;

namespace Application.Services.Moderation
{
    // Core moderation engine: blacklist pre-check, OpenAI score analysis, age rating.
    public class ModerationService : IModerationService
    {
        private readonly IBlacklistProvider _blacklist;

        private static readonly Dictionary<char, char> TeencodeMap = new()
        {
            { '@', 'a' }, { '1', 'i' }, { '0', 'o' }, { '3', 'e' },
            { '!', 'i' }, { '$', 's' }, { '4', 'a' }
        };

        public ModerationService(IBlacklistProvider blacklist)
        {
            _blacklist = blacklist;
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

        // Analyze OpenAI scores against thresholds. Zero-tolerance for CSAM.
        public OpenAiScoreResponse AnalyzeOpenAiScores(OpenAiScoreRequest request)
        {
            if (request.Scores.TryGetValue("sexual/minors", out double csamScore) && csamScore >= 0.3)
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

            foreach (var kvp in request.Scores)
            {
                if (!_blacklist.Thresholds.TryGetValue(kvp.Key, out var rule)) continue;

                ModerationActionType currentAction;
                if (kvp.Value >= rule.AUTO_REJECT)
                    currentAction = ModerationActionType.AutoReject;
                else if (kvp.Value >= rule.FLAG_FOR_REVIEW)
                    currentAction = ModerationActionType.FlagForReview;
                else
                    currentAction = ModerationActionType.AutoPass;

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

        private static bool ContainsWord(string text, BlacklistEntry entry)
        {
            if (text.Contains(entry.Word, StringComparison.OrdinalIgnoreCase))
                return true;

            foreach (var variant in entry.Variants)
            {
                if (text.Contains(variant, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
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
    }
}
