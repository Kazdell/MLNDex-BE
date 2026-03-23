using System;

namespace Domain.Entities
{
  public class UserListItem
  {
    public int UserListItemId { get; set; }
    public int UserListId { get; set; }
    public int SeriesId { get; set; }
    public DateTime AddedAt { get; set; }

    public UserList UserList { get; set; } = null!;
    public Series Series { get; set; } = null!;
  }
}
