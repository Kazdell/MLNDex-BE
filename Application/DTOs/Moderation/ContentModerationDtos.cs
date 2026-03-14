using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Moderation
{
    public class ContentModerationDecisionRequest
    {
        [Required]
        public ContentDecisionAction Action { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public enum ContentDecisionAction
    {
        APPROVE,
        REJECT,
        BAN,
    }
}
