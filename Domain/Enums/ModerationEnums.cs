namespace Domain.Enums
{
    /// <summary>
    /// Action decided by the moderation engine after analyzing content.
    /// </summary>
    public enum ModerationAction
    {
        /// <summary>Content is clean, publish immediately.</summary>
        AutoPass = 0,

        /// <summary>Content is suspicious, queue for human moderator review.</summary>
        FlagForReview = 1,

        /// <summary>Content clearly violates policy, reject automatically.</summary>
        AutoReject = 2,

        /// <summary>Content triggers zero-tolerance rule (CSAM), ban user instantly.</summary>
        InstantBan = 3
    }

    /// <summary>
    /// Severity level of a blacklist word or violation.
    /// Maps directly to blacklist.json "severity" field.
    /// </summary>
    public enum ContentSeverity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Extreme = 3
    }

    /// <summary>
    /// Age rating assigned to content based on OpenAI scores.
    /// Aligned with Webtoon/MangaDex standards.
    /// </summary>
    public enum AgeRating
    {
        /// <summary>Safe for all audiences.</summary>
        AllAges = 0,

        /// <summary>Contains mild violence or suggestive themes.</summary>
        Teen13 = 13,

        /// <summary>Contains moderate violence, blood, or partial nudity.</summary>
        Mature16 = 16,

        /// <summary>Contains explicit content, restricted to web-only (Webtoon policy).</summary>
        Adult18 = 18
    }
}
