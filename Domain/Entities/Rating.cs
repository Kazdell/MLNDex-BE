using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class Rating
  {
    public int RatingId { get; set; }
    public int UserId { get; set; }
    public int SeriesId { get; set; }
    public int Score { get; set; }
    public string? Review { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public Series Series { get; set; } = null!;
  }
}
