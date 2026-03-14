using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Moderation
{
    public class AccountActionRequest
    {
        [Required]
        public AccountActionType Action { get; set; }

        [StringLength(500)]
        public string? Reason { get; set; }
    }

    public enum AccountActionType
    {
        WARN,
        DEACTIVATE,
        ACTIVATE,
    }

    public class AccountActionResponse
    {
        public int UserId { get; set; }
        public bool IsActive { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
