using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Community
{
    public class LikeRequest
    {
        [Required]
        public Domain.Entities.LikeTargetType TargetType { get; set; }

        [Range(1, int.MaxValue)]
        public int TargetId { get; set; }
    }

    public class LikeResponse
    {
        public bool Liked { get; set; }
        public int TotalLikes { get; set; }
    }
}
