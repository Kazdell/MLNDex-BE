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

        Task<List<SeriesListItemDto>> GetByCreatorAsync(
            int creatorId, 
            CancellationToken cancellationToken = default);
    }
}
