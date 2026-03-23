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
      var from = request.From ?? DateTime.MinValue;
      var to = request.To ?? DateTime.MaxValue;

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
              u.Transaction.CreatedAt >= from && u.Transaction.CreatedAt <= to
          )
          .CountAsync(cancellationToken);

      var creatorEarningsQuery = _context
          .ChapterUnlocks.Include(u => u.Chapter)
              .ThenInclude(c => c.Series)
                  .ThenInclude(s => s.Creator)
          .Where(u => u.Transaction.CreatedAt >= from && u.Transaction.CreatedAt <= to)
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
        },
        TopCreators = topCreators,
      };
    }
  }
}
