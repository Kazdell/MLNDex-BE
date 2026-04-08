using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Microsoft.Extensions.Caching.Memory;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Creator
{
  public class GenreService : IGenreService
  {
    private readonly IMlndexDbContext _context;
    private readonly IMemoryCache _cache;

    public GenreService(IMlndexDbContext context, IMemoryCache cache)
    {
      _context = context;
      _cache = cache;
    }

    public async Task<IEnumerable<Genre>> GetAllGenresAsync()
    {
      return await _cache.GetOrCreateAsync("AllGenres", async entry => 
      {
          entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2);
          return await _context.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync();
      });
    }

    public async Task<Genre?> GetGenreByIdAsync(int id)
    {
      return await _context.Genres.AsNoTracking().FirstOrDefaultAsync(g => g.GenreId == id);
    }
  }
}
