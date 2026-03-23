using System;
using System.Collections.Generic;

namespace Application.DTOs.UserList
{
  public class UserListDto
  {
    public int UserListId { get; set; }
    public int UserId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPublic { get; set; }
    public int ItemCount { get; set; }
  }

  public class UserListDetailDto : UserListDto
  {
    public List<UserListItemDto> Items { get; set; } = new List<UserListItemDto>();
  }

  public class CreateUserListDto
  {
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
  }

  public class UpdateUserListDto
  {
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsPublic { get; set; }
  }

  public class UserListItemDto
  {
    public int UserListItemId { get; set; }
    public int UserListId { get; set; }
    public int SeriesId { get; set; }
    public DateTime AddedAt { get; set; }

    // Basic Series Info for displaying the item
    public string SeriesTitle { get; set; } = string.Empty;
    public string? SeriesCoverUrl { get; set; }
    public string SeriesStatus { get; set; } = string.Empty;
    public string SeriesFormat { get; set; } = string.Empty;
  }

  public class AddUserListItemDto
  {
    public int SeriesId { get; set; }
  }
}
