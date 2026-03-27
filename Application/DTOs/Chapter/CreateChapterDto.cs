using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs.Chapter
{
    public class CreateChapterDto
    {
        public int SeriesId { get; set; }
        public float ChapterNumber { get; set; }
        public string? Title { get; set; }
        public int? LanguageId { get; set; }
        public int? TeamId { get; set; }
        public List<UploadPageDto>? Pages { get; set; }

        // --- Translation specific fields ---
        public int? BaseChapterId { get; set; } // The ID of the original chapter being translated
        public int? PermissionId { get; set; } // The permission ID under which this is uploaded
        public string? CreditsJson { get; set; } // JSON string of translation credits (roles and users/names)
        public string? JointTeamIdsJson { get; set; } // JSON string of joint team IDs

        // --- Unlock fields ---
        public ChapterLockStatus? LockStatus { get; set; }
        public int? UnlockPriceCoins { get; set; }
        public int? FreeAfterDays { get; set; }
    }
}
