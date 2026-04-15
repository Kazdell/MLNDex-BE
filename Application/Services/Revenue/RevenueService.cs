using Application.DTOs.Common;
using Application.DTOs.Revenue;
using Application.DTOs.Revenue.Request;
using Application.DTOs.Revenue.Response;
using Application.Exceptions;
using Application.Interfaces.Data;
using Application.Interfaces.Revenue;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Revenue
{
    public class RevenueService : IRevenueService
    {
        private readonly IMlndexDbContext _db;

        public RevenueService(IMlndexDbContext db)
        {
            _db = db;
        }

        public async Task<CreatorRevenueSummaryDto> GetCreatorRevenueAsync(
            int userId, RevenueQueryDto query, CancellationToken ct = default)
        {
            _ = await _db.CreatorProfiles
                .FirstOrDefaultAsync(c => c.UserId == userId, ct)
                ?? throw new AppException(ErrorCodes.OPERATION_NOT_ALLOWED);

            var toExclusive = query.To.Date.AddDays(1);

            // Chỉ lấy AUTHOR_ROYALTY — không cần filter Note nữa
            var transactions = await _db.Transactions
                .Where(t => t.UserId == userId
                    && t.Type == TransactionType.AUTHOR_ROYALTY
                    && t.CreatedAt >= query.From
                    && t.CreatedAt < toExclusive)
                .ToListAsync(ct);

            var seriesIds = transactions
                .Where(t => t.RelatedSeriesId.HasValue)
                .Select(t => t.RelatedSeriesId!.Value)
                .Distinct()
                .ToList();

            var seriesTitleMap = await _db.Series
                .Where(s => seriesIds.Contains(s.SeriesId))
                .Select(s => new { s.SeriesId, s.Title })
                .ToDictionaryAsync(s => s.SeriesId, s => s.Title, ct);

            var bySeries = transactions
                .Where(t => t.RelatedSeriesId.HasValue)
                .GroupBy(t => t.RelatedSeriesId!.Value)
                .Select(g => new RevenueBySeriesDto
                {
                    SeriesId = g.Key,
                    SeriesTitle = seriesTitleMap.GetValueOrDefault(g.Key, $"Series #{g.Key}"),
                    UnlockCount = g.Count(),
                    Revenue = g.Sum(t => t.AmountCoins)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            var total = transactions.Sum(t => t.AmountCoins);
            var periodCount = GetPeriodCount(query.From, query.To, query.Granularity);

            return new CreatorRevenueSummaryDto
            {
                TotalRevenue = total,
                TotalUnlocks = transactions.Count,
                AveragePerPeriod = periodCount > 0 ? Math.Round(total / periodCount, 2) : 0,
                DataPoints = GroupByGranularity(transactions, query.From, query.To, query.Granularity),
                BySeries = bySeries,
            };
        }

        public async Task<SeriesRevenueSummaryDto> GetSeriesRevenueAsync(
    int userId, int seriesId, RevenueQueryDto query, CancellationToken ct = default)
        {
            var series = await _db.Series
                .Include(s => s.Creator)
                .FirstOrDefaultAsync(s => s.SeriesId == seriesId && s.Creator.UserId == userId, ct)
                ?? throw new AppException(ErrorCodes.SERIES_NOT_FOUND);

            var toExclusive = query.To.Date.AddDays(1);

            // ← Dùng Transaction thay vì ChapterUnlock để đồng nhất với GetCreatorRevenueAsync
            var transactions = await _db.Transactions
                .Where(t => t.UserId == userId
                    && t.Type == TransactionType.AUTHOR_ROYALTY
                    && t.RelatedSeriesId == seriesId
                    && t.CreatedAt >= query.From
                    && t.CreatedAt < toExclusive)
                .ToListAsync(ct);

            var total = transactions.Sum(t => t.AmountCoins);
            var periodCount = GetPeriodCount(query.From, query.To, query.Granularity);

            return new SeriesRevenueSummaryDto
            {
                SeriesId = seriesId,
                SeriesTitle = series.Title,
                TotalRevenue = total,
                TotalUnlocks = transactions.Count,
                AveragePerPeriod = periodCount > 0 ? Math.Round(total / periodCount, 2) : 0,
                DataPoints = GroupByGranularity(transactions, query.From, query.To, query.Granularity),
            };
        }

        public async Task<TeamRevenueSummaryDto> GetTeamRevenueAsync(
    int userId, int teamId, RevenueQueryDto query, CancellationToken ct = default)
        {
            _ = await _db.TranslationTeams
                .FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == userId, ct)
                ?? throw new AppException(ErrorCodes.TEAM_NOT_FOUND);

            var toExclusive = query.To.Date.AddDays(1);

            var transactions = await _db.Transactions
                .Where(t => t.UserId == userId
                    && t.Type == TransactionType.TEAM_ROYALTY
                    && t.RelatedEntityId == teamId
                    && t.CreatedAt >= query.From
                    && t.CreatedAt < toExclusive)
                .ToListAsync(ct);

            // ── THÊM: group by series ──────────────────────────────────────
            var seriesIds = transactions
                .Where(t => t.RelatedSeriesId.HasValue)
                .Select(t => t.RelatedSeriesId!.Value)
                .Distinct()
                .ToList();

            var seriesTitleMap = await _db.Series
                .Where(s => seriesIds.Contains(s.SeriesId))
                .Select(s => new { s.SeriesId, s.Title })
                .ToDictionaryAsync(s => s.SeriesId, s => s.Title, ct);

            var bySeries = transactions
                .Where(t => t.RelatedSeriesId.HasValue)
                .GroupBy(t => t.RelatedSeriesId!.Value)
                .Select(g => new RevenueBySeriesDto
                {
                    SeriesId = g.Key,
                    SeriesTitle = seriesTitleMap.GetValueOrDefault(g.Key, $"Series #{g.Key}"),
                    UnlockCount = g.Count(),
                    Revenue = g.Sum(t => t.AmountCoins)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();
            // ───────────────────────────────────────────────────────────────

            var total = transactions.Sum(t => t.AmountCoins);
            var periodCount = GetPeriodCount(query.From, query.To, query.Granularity);

            return new TeamRevenueSummaryDto
            {
                TotalRevenue = total,
                TotalUnlocks = transactions.Count,
                AveragePerPeriod = periodCount > 0 ? Math.Round(total / periodCount, 2) : 0,
                DataPoints = GroupByGranularity(transactions, query.From, query.To, query.Granularity),
                BySeries = bySeries,   // ← THÊM
            };
        }

        public async Task<SeriesRevenueSummaryDto> GetTeamSeriesRevenueAsync(
            int userId, int teamId, int seriesId, RevenueQueryDto query, CancellationToken ct = default)
        {
            _ = await _db.TranslationTeams
                .FirstOrDefaultAsync(t => t.TeamId == teamId && t.LeaderId == userId, ct)
                ?? throw new AppException(ErrorCodes.TEAM_NOT_FOUND);

            var series = await _db.Series
                .FirstOrDefaultAsync(s => s.SeriesId == seriesId, ct)
                ?? throw new AppException(ErrorCodes.SERIES_NOT_FOUND);

            var toExclusive = query.To.Date.AddDays(1);

            // Dùng RelatedSeriesId thay vì filter Note
            var transactions = await _db.Transactions
                .Where(t => t.UserId == userId
                    && t.Type == TransactionType.TEAM_ROYALTY
                    && t.RelatedEntityId == teamId
                    && t.RelatedSeriesId == seriesId          // ← thay Note.Contains
                    && t.CreatedAt >= query.From
                    && t.CreatedAt < toExclusive)
                .ToListAsync(ct);

            var total = transactions.Sum(t => t.AmountCoins);
            var periodCount = GetPeriodCount(query.From, query.To, query.Granularity);

            return new SeriesRevenueSummaryDto
            {
                SeriesId = seriesId,
                SeriesTitle = series.Title,
                TotalRevenue = total,
                TotalUnlocks = transactions.Count,
                AveragePerPeriod = periodCount > 0 ? Math.Round(total / periodCount, 2) : 0,
                DataPoints = GroupByGranularity(transactions, query.From, query.To, query.Granularity),
            };
        }

        // ── HELPERS ──────────────────────────────────────────────────────────

        private static List<RevenueDataPointDto> GroupByGranularity(
            List<Transaction> transactions, DateTime from, DateTime to, string granularity)
        {
            var result = new List<RevenueDataPointDto>();
            var current = from.Date;
            var toDate = to.Date;

            while (current <= toDate)
            {
                string label;
                IEnumerable<Transaction> group;

                if (granularity == "year")
                {
                    label = current.Year.ToString();
                    group = transactions.Where(t => t.CreatedAt.Year == current.Year);
                    current = current.AddYears(1);
                }
                else if (granularity == "month")
                {
                    label = current.ToString("yyyy-MM");
                    group = transactions.Where(t =>
                        t.CreatedAt.Year == current.Year &&
                        t.CreatedAt.Month == current.Month);
                    current = current.AddMonths(1);
                }
                else
                {
                    label = current.ToString("yyyy-MM-dd");
                    group = transactions.Where(t => t.CreatedAt.Date == current);
                    current = current.AddDays(1);
                }

                var list = group.ToList();
                result.Add(new RevenueDataPointDto
                {
                    Label = label,
                    Amount = list.Sum(t => t.AmountCoins),
                    UnlockCount = list.Count,
                });
            }

            return result;
        }

        private static List<RevenueDataPointDto> GroupByGranularityFromUnlocks(
            List<ChapterUnlock> unlocks, DateTime from, DateTime to, string granularity)
        {
            var result = new List<RevenueDataPointDto>();
            var current = from.Date;
            var toDate = to.Date;

            while (current <= toDate)
            {
                string label;
                IEnumerable<ChapterUnlock> group;

                if (granularity == "year")
                {
                    label = current.Year.ToString();
                    group = unlocks.Where(u => u.CreatedAt.Year == current.Year);
                    current = current.AddYears(1);
                }
                else if (granularity == "month")
                {
                    label = current.ToString("yyyy-MM");
                    group = unlocks.Where(u =>
                        u.CreatedAt.Year == current.Year &&
                        u.CreatedAt.Month == current.Month);
                    current = current.AddMonths(1);
                }
                else
                {
                    label = current.ToString("yyyy-MM-dd");
                    group = unlocks.Where(u => u.CreatedAt.Date == current);
                    current = current.AddDays(1);
                }

                var list = group.ToList();
                result.Add(new RevenueDataPointDto
                {
                    Label = label,
                    Amount = list.Sum(u => u.CoinsPaid),
                    UnlockCount = list.Count,
                });
            }

            return result;
        }

        private static decimal GetPeriodCount(DateTime from, DateTime to, string granularity)
        {
            return granularity switch
            {
                "year" => to.Year - from.Year + 1,
                "month" => ((to.Year - from.Year) * 12) + to.Month - from.Month + 1,
                _ => Math.Max(1, (decimal)(to.Date - from.Date).TotalDays + 1)
            };
        }
    }
}