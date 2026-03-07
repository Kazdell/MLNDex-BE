using System;

namespace Domain.Entities
{
    public class Like
    {
        public int LikeId { get; set; }
        public int UserId { get; set; }
        public int TargetId { get; set; }
        public LikeTargetType TargetType { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation
        public User User { get; set; } = null!;
    }

    public enum LikeTargetType
    {
        SERIES,
        CHAPTER,
        COMMENT,
    }
}
