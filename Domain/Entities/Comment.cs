using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class Comment
  {
    public int CommentId { get; set; }
    public int UserId { get; set; }
    public int TargetId { get; set; }
    public CommentTargetType TargetType { get; set; }
    public string Content { get; set; } = null!;
    public int? ParentCommentId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Comment? ParentComment { get; set; }
    public ICollection<Comment> Replies { get; set; } = new List<Comment>();
  }

  public enum CommentTargetType
  {
    SERIES,
    CHAPTER
  }
}
