using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
    public class CreatorProfile
    {
        public int CreatorId { get; set; }
        public int UserId { get; set; }
        public string PenName { get; set; } = null!;
        public int ReputationScore { get; set; }
        public decimal TotalRevenue { get; set; }
        public bool HideRevenue { get; set; }
        public bool IsActive { get; set; }
        public ModerationStatus ModerationStatus { get; set; }

        // Early unlock defaults 
        public bool UnlockEnabled { get; set; } = false;
        public int? DefaultUnlockPriceCoins { get; set; }
        public bool FreeAfterEnabled { get; set; } = false;
        public int? DefaultFreeAfterDays { get; set; }

        // Navigation
        public User User { get; set; } = null!;
        public ICollection<Series> Series { get; set; } = new List<Series>();
        public ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
    }

    public enum ModerationStatus
    {
        PENDING,
        APPROVED,
        REJECTED,
        BANNED
    }
}
