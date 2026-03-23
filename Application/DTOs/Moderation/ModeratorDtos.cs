using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Moderation
{
  public class ModeratorListRequest
  {
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 20;

    public string? Keyword { get; set; }

    public bool? IsActive { get; set; }
  }

  public class ModeratorDto
  {
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
  }

  public class ModeratorListResponse
  {
    public List<ModeratorDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
  }

  public class AssignModeratorRequest
  {
    [Required]
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }
  }
}
