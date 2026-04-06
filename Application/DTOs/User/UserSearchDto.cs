namespace Application.DTOs.User
{
  public class UserSearchDto
  {
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Avatar { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool IsActive { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
  }
}
