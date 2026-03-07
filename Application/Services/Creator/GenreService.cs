using Application.Interfaces.Creator;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Creator
{
    public class GenreService : IGenreService
    {
        private readonly IMlndexDbContext _context;

        public GenreService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<Genre>> GetAllGenresAsync()
        {
            return await _context.Genres.AsNoTracking().OrderBy(g => g.Name).ToListAsync();
        }

        public async Task<Genre?> GetGenreByIdAsync(int id)
        {
            return await _context.Genres.AsNoTracking().FirstOrDefaultAsync(g => g.GenreId == id);
        }
    }
}
