using Application.DTOs.Series;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces.Series
{
    public interface ISeriesService
    {
        Task<PaginatedList<SeriesDto>> GetSeriesListAsync(string sortBy = "newest", int page = 1, int pageSize = 20);
        Task<PaginatedList<SeriesDto>> SearchSeriesAsync(SeriesSearchRequest request);
        Task<SeriesDetailDto?> GetSeriesDetailsAsync(int seriesId);
        Task<List<SeriesDto>> GetRecommendationsAsync(int userId, int limit = 10);
    }
}
