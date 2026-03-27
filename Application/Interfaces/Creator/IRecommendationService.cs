using Application.DTOs.Creator;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Interfaces.Creator
{
    public interface IRecommendationService
    {
        Task<List<SeriesDto>> GetRecommendationsAsync(int userId, int limit = 10, int? currentSeriesId = null);
    }
}
