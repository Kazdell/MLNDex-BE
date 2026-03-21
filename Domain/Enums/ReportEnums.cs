namespace Domain.Enums
{
    public enum ReportTargetType
    {
        Series,
        ChapterTranslation,
        Team,
        User
    }

    public enum ReportReason
    {
        Plagiarism,
        MTL_Abuse,
        Unauthorized_Reup,
        Inappropriate,
        Other
    }

    public enum ReportStatus
    {
        Pending,
        Investigating,
        Resolved,
        Rejected
    }

    public enum TrustScoreTargetType
    {
        User,
        Team
    }
}
