using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class SeriesGenre
  {
    public int SeriesGenreId { get; set; }
    public int SeriesId { get; set; }
    public int GenreId { get; set; }

    // Navigation
    public Series Series { get; set; } = null!;
    public Genre Genre { get; set; } = null!;
  }
}
