using System;
using System.Collections.Generic;

namespace Domain.Entities
{
  public class UserList
  {
    public int UserListId { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPublic { get; set; }

    public User User { get; set; } = null!;
    public ICollection<UserListItem> Items { get; set; } = new List<UserListItem>();
  }
}
