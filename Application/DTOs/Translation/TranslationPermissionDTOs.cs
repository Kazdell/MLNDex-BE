using System;
using Domain.Entities;

namespace Application.DTOs.Translation
{
    public class RequestPermissionDto
    {
        public int SeriesId { get; set; }
        public int TeamId { get; set; }
        public string? Note { get; set; }
    }

    public class ReviewPermissionDto
    {
        public bool IsApproved { get; set; }
    }

    public class TranslationPermissionDto
    {
        public int PermissionId { get; set; }
        public int SeriesId { get; set; }
        public string? SeriesTitle { get; set; }
        public int TeamId { get; set; }
        public string? TeamName { get; set; }
        public int GrantedBy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public DateTime? GrantedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? Note { get; set; }
        
        // Team Details for Review
        public string? Facebook { get; set; }
        public string? Discord { get; set; }
        public string? Website { get; set; }
        public string? Certificates { get; set; }
    }
}
