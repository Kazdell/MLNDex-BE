using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace Application.DTOs.Community
{
    public class CreateCommentRequest
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int TargetId { get; set; }

        [Required]
        public CommentTargetType TargetType { get; set; }

        [Required]
        [StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;

        public int? ParentCommentId { get; set; }
    }

    public class CommentDto
    {
        public int CommentId { get; set; }
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Content { get; set; } = string.Empty;
        public int? ParentCommentId { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<CommentDto> Replies { get; set; } = new();
    }

    public class CommentListResponse
    {
        public List<CommentDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
