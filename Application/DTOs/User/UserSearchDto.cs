namespace Application.DTOs.User
{
  public class UserSearchDto
  {
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Avatar { get; set; }
  }
}
