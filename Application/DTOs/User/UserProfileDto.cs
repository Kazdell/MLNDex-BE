using System;
using System.Collections.Generic;

namespace Application.DTOs.User
{
    public class UserProfileDto
    {
        public int UserId { get; set; }
        public string Username { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? Avatar { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public string? BannerUrl { get; set; }

        
        // Stats
        public int TotalReadSeries { get; set; }
        public int TotalReadChapters { get; set; }
        public int TotalCreatedSeries { get; set; }
        public int FollowersCount { get; set; }
        public int FollowingCount { get; set; }
        public decimal WalletBalance { get; set; }
        public string SubscriptionType { get; set; } = "Cơ bản";
    }

    public class UpdateProfileDto
    {
        public string? DisplayName { get; set; }
        public string? Bio { get; set; }
        public string? Avatar { get; set; }
        public string? BannerUrl { get; set; }
    }
}

