using Domain.Enums;

namespace Domain.Entities.Moderation
{
    /// <summary>
    /// Value Object representing the outcome of a moderation analysis.
    /// Returned by both PreCheck (text/blacklist) and OpenAI threshold analysis.
    /// </summary>
    public class ModerationResult
    {
        /// <summary>The final decision: Pass, Flag, Reject, or InstantBan.</summary>
        public ModerationAction Action { get; set; }

        /// <summary>Human-readable reasons why this decision was made.</summary>
        public List<string> Reasons { get; set; } = new();

        /// <summary>Total penalty points accumulated from blacklist matches.</summary>
        public int PenaltyPoints { get; set; }

        /// <summary>ID of the rejection template to send to the user (e.g. "REJ_013").</summary>
        public string? TemplateId { get; set; }

        /// <summary>Suggested age rating based on content analysis.</summary>
        public AgeRating? SuggestedAgeRating { get; set; }

        /// <summary>Whether the user account should be permanently banned.</summary>
        public bool IsPermaBan { get; set; }

        /// <summary>
        /// Factory method for a clean pass result.
        /// </summary>
        public static ModerationResult Pass()
        {
            return new ModerationResult { Action = ModerationAction.AutoPass };
        }

        /// <summary>
        /// Factory method for a flag-for-review result.
        /// </summary>
        public static ModerationResult Flag(List<string> reasons, int penaltyPoints, string? templateId = null)
        {
            return new ModerationResult
            {
                Action = ModerationAction.FlagForReview,
                Reasons = reasons,
                PenaltyPoints = penaltyPoints,
                TemplateId = templateId
            };
        }

        /// <summary>
        /// Factory method for an auto-reject result.
        /// </summary>
        public static ModerationResult Reject(string reason, int penaltyPoints, string templateId, bool isPermaBan = false)
        {
            return new ModerationResult
            {
                Action = isPermaBan ? ModerationAction.InstantBan : ModerationAction.AutoReject,
                Reasons = new List<string> { reason },
                PenaltyPoints = penaltyPoints,
                TemplateId = templateId,
                IsPermaBan = isPermaBan
            };
        }
    }
}
