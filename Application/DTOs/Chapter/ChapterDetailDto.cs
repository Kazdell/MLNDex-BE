using Domain.Entities;
using System;
using System.Collections.Generic;

namespace Application.DTOs.Chapter
{
    public class ChapterDetailDto
    {
        public int ChapterId { get; set; }
        public int SeriesId { get; set; }
        public string? SeriesTitle { get; set; }
        public string? UploaderName { get; set; }
        public int? CreatorUserId { get; set; }
        public string? TranslatorTeamName { get; set; }
        public float ChapterNumber { get; set; }
        public string? Title { get; set; }
        public int? PrevChapterId { get; set; }
        public int? NextChapterId { get; set; }
        public List<ChapterPageResponseDto> Pages { get; set; } = new();
        public List<ChapterSummaryDto> Chapters { get; set; } = new();
        public string ModerationStatus { get; set; } = string.Empty;
        public string? Language { get; set; }
        public string? ModerationReason { get; set; }

        // ── Unlock settings ───────────────────────────────────────────────
        public string LockStatus { get; set; } = "UNLOCKED";
        public bool IsUnlockedByUser { get; set; } = false;
        public int? UnlockPriceCoins { get; set; }
        public DateTime? UnlockTime { get; set; }
        public CreatorUnlockDefaultsDto? CreatorDefaults { get; set; }


        // ── Translation Ecosystem ─────────────────────────────────────────
        public bool IsTranslation { get; set; }
        public bool IsOfficial { get; set; }
        public bool IsOutdated { get; set; }
        public bool IsOrphan { get; set; }
        public List<TranslationCreditDetailDto>? TranslationCredits { get; set; }
        public List<JointTeamDetailDto>? JointTeams { get; set; }
    }

    public class CreatorUnlockDefaultsDto
    {
        public bool UnlockEnabled { get; set; }
        public int? DefaultUnlockPriceCoins { get; set; }
        public bool FreeAfterEnabled { get; set; }
        public int? DefaultFreeAfterDays { get; set; }
    }

    public class TranslationCreditDetailDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class JointTeamDetailDto
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
    }
}