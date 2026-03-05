using Domain.Entities;

namespace Application.Interfaces.Creator
{
    public interface IGenreService
    {
        Task<IEnumerable<Genre>> GetAllGenresAsync();
        Task<Genre> GetGenreByIdAsync(int id);
    }
}
