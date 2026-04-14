using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Entities
{
  public class Series
  {
    public int SeriesId { get; set; }
    public int CreatorId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }
    public SeriesFormat SeriesFormat { get; set; }
    public AgeRating AgeRating { get; set; }
    public SeriesStatus Status { get; set; }
    public ModerationStatus ModerationStatus { get; set; }
    public int ViolenceScore { get; set; }
    public int NudityScore { get; set; }
    public int SexualScore { get; set; }
    public int LanguageScore { get; set; }
    public int SubstancesScore { get; set; }
    public int SensitiveScore { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public CreatorProfile Creator { get; set; } = null!;
    public ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
    public ICollection<SeriesGenre> SeriesGenres { get; set; } = new List<SeriesGenre>();
    public ICollection<ReadingHistory> ReadingHistories { get; set; } = new List<ReadingHistory>();
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
    public ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public ICollection<TranslationPermission> TranslationPermissions { get; set; } = new List<TranslationPermission>();
  }

  public enum SeriesFormat
  {
    MANGA,
    NOVEL,
    COMIC
  }

  public enum AgeRating
  {
    ALL,        // Legacy value stored in DB
    ALL_AGES,
    TEEN,
    MATURE,
    ADULT
  }

  public enum SeriesStatus
  {
    ONGOING,
    COMPLETED,
    HALTED,
    DROPPED
  }
}
