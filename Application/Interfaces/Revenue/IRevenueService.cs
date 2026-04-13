using Application.DTOs.Revenue.Request;
using Application.DTOs.Revenue.Response;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces.Revenue
{
    public interface IRevenueService
    {
        Task<CreatorRevenueSummaryDto> GetCreatorRevenueAsync(
            int userId, RevenueQueryDto query, CancellationToken ct = default);

        Task<SeriesRevenueSummaryDto> GetSeriesRevenueAsync(
            int userId, int seriesId, RevenueQueryDto query, CancellationToken ct = default);

        Task<TeamRevenueSummaryDto> GetTeamRevenueAsync(
            int userId, int teamId, RevenueQueryDto query, CancellationToken ct = default);
        Task<SeriesRevenueSummaryDto> GetTeamSeriesRevenueAsync(
    int userId, int teamId, int seriesId, RevenueQueryDto query, CancellationToken ct = default);
    }
}