using Application.DTOs.Creator;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Creator
{
    public interface ISeriesService
    {
        Task<CreateSeriesResponseDto> CreateAsync(
            int creatorId,
            CreateSeriesDto dto,
            CancellationToken cancellationToken = default);

        Task<CreateSeriesResponseDto> UpdateAsync(
            int seriesId,
            int creatorId,
            CreateSeriesDto dto,
            CancellationToken cancellationToken = default);


        Task<List<SeriesListItemDto>> GetByCreatorAsync(
            int creatorId, 
            CancellationToken cancellationToken = default);

        Task<PaginatedList<SeriesDto>> GetSeriesListAsync(string sortBy = "newest", int page = 1, int pageSize = 20);
        Task<PaginatedList<SeriesDto>> SearchSeriesAsync(SeriesSearchRequest request);
        Task<SeriesDetailDto?> GetSeriesDetailsAsync(int seriesId);
        Task<List<SeriesDto>> GetRecommendationsAsync(int userId, int limit = 10);
        Task<CreateSeriesDto?> GetForEditAsync(int seriesId, int creatorId);
    }
}

