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
        public int TeamId { get; set; }
        public int GrantedBy { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime? GrantedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? Note { get; set; }
    }
}
