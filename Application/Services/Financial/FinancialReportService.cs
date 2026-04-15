using Application.DTOs.Financial;
using Application.Interfaces.Data;
using Application.Interfaces.Financial;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Financial
{
    public class FinancialReportService : IFinancialReportService
    {
        private readonly IMlndexDbContext _context;

        public FinancialReportService(IMlndexDbContext context)
        {
            _context = context;
        }

        public async Task<FinancialReportResponse> GetSummaryAsync(
            FinancialReportRequest request,
            CancellationToken cancellationToken = default
        )
        {
            var from = request.From ?? DateTime.UtcNow.Date.AddDays(-29);
            var to = request.To ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            var config = await _context.SystemConfigs.FirstOrDefaultAsync(cancellationToken);
            var exchangeRate = config?.ExchangeRateCoinToVnd ?? 1000m;

            var totalCoinPurchased =
                await _context
                    .Transactions.Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
                    .Where(t => t.Status == TransactionStatus.COMPLETED)
                    .Where(t => t.Type == TransactionType.PURCHASE_COIN)
                    .SumAsync(t => (decimal?)t.AmountCoins, cancellationToken) ?? 0m;

            var totalWithdrawCoins =
                await _context
                    .WithdrawalRequests.Where(w => w.ProcessedAt >= from && w.ProcessedAt <= to)
                    .Where(w => w.Status == WithdrawalStatus.COMPLETED)
                    .SumAsync(w => (decimal?)w.AmountCoins, cancellationToken) ?? 0m;

            var totalUnlocks = await _context
                .ChapterUnlocks.Where(u =>
                    u.Transaction!.CreatedAt >= from && u.Transaction!.CreatedAt <= to
                )
                .CountAsync(cancellationToken);

            var purchasedDaily = await _context
                .Transactions.Where(t => t.CreatedAt >= from && t.CreatedAt <= to)
                .Where(t => t.Status == TransactionStatus.COMPLETED)
                .Where(t => t.Type == TransactionType.PURCHASE_COIN)
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Amount = g.Sum(x => (decimal?)x.AmountCoins) ?? 0m,
                })
                .ToListAsync(cancellationToken);

            var withdrawnDaily = await _context
                .WithdrawalRequests.Where(w =>
                    w.ProcessedAt.HasValue
                    && w.ProcessedAt.Value >= from
                    && w.ProcessedAt.Value <= to
                )
                .Where(w => w.Status == WithdrawalStatus.COMPLETED)
                .GroupBy(w => w.ProcessedAt!.Value.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Amount = g.Sum(x => (decimal?)x.AmountCoins) ?? 0m,
                })
                .ToListAsync(cancellationToken);

            var purchasedMap = purchasedDaily.ToDictionary(x => x.Date, x => x.Amount);
            var withdrawnMap = withdrawnDaily.ToDictionary(x => x.Date, x => x.Amount);
            var dailyRevenue = new List<DailyRevenueDto>();

            for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            {
                purchasedMap.TryGetValue(day, out var purchased);
                withdrawnMap.TryGetValue(day, out var withdrawn);
                dailyRevenue.Add(
                    new DailyRevenueDto
                    {
                        Date = day.ToString("yyyy-MM-dd"),
                        Purchased = purchased,
                        Withdrawn = withdrawn,
                    }
                );
            }

            var creatorEarningsQuery = _context
                .ChapterUnlocks.Include(u => u.Chapter)
                    .ThenInclude(c => c.Series)
                        .ThenInclude(s => s.Creator)
                .Where(u => u.Transaction!.CreatedAt >= from && u.Transaction!.CreatedAt <= to)
                .GroupBy(u => new { u.Chapter.Series.CreatorId, u.Chapter.Series.Creator.PenName })
                .Select(g => new CreatorRevenueDto
                {
                    CreatorId = g.Key.CreatorId,
                    CreatorName = g.Key.PenName,
                    CoinsEarned = g.Sum(x => x.CoinsPaid),
                    UnlockCount = g.Count(),
                })
                .OrderByDescending(c => c.CoinsEarned)
                .Take(request.TopCreators);

            var topCreators = await creatorEarningsQuery.ToListAsync(cancellationToken);

            return new FinancialReportResponse
            {
                Summary = new FinancialSummaryDto
                {
                    TotalCoinPurchased = totalCoinPurchased,
                    TotalWithdrawCoins = totalWithdrawCoins,
                    TotalUnlocks = totalUnlocks,
                    TotalCoinPurchasedVnd = totalCoinPurchased * exchangeRate,
                    TotalWithdrawVnd = totalWithdrawCoins * exchangeRate,
                    NetCoins = totalCoinPurchased - totalWithdrawCoins,
                    NetVnd = (totalCoinPurchased - totalWithdrawCoins) * exchangeRate,
                    ExchangeRateUsed = exchangeRate,
                },
                TopCreators = topCreators,
                DailyRevenue = dailyRevenue,
            };
        }
    }
}
