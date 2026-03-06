using Application.DTOs.Financial;
using Application.Interfaces.Data;
using Application.Interfaces.Financial;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services.Financial
{
    public class WithdrawalService : IWithdrawalService
    {
        private readonly IMlndexDbContext _context;
        private readonly ILogger<WithdrawalService> _logger;

        public WithdrawalService(IMlndexDbContext context, ILogger<WithdrawalService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<WithdrawalReviewListResponse> GetPendingAsync(
            WithdrawalReviewListRequest request,
            CancellationToken cancellationToken = default
        )
        {
            var query = _context.WithdrawalRequests.Include(w => w.Creator).AsQueryable();

            if (request.Status.HasValue)
            {
                query = query.Where(w => w.Status == request.Status.Value);
            }
            else
            {
                query = query.Where(w =>
                    w.Status == WithdrawalStatus.PENDING || w.Status == WithdrawalStatus.PROCESSING
                );
            }

            if (request.CreatorId.HasValue)
            {
                query = query.Where(w => w.CreatorId == request.CreatorId.Value);
            }

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .OrderByDescending(w => w.RequestedAt)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(w => new WithdrawalReviewItemDto
                {
                    WithdrawalId = w.WithdrawalId,
                    CreatorId = w.CreatorId,
                    CreatorName = w.Creator.PenName,
                    AmountCoins = w.AmountCoins,
                    AmountVnd = w.AmountVnd,
                    BankAccountInfo = w.BankAccountInfo,
                    RequestedAt = w.RequestedAt,
                    ProcessedAt = w.ProcessedAt,
                    Status = w.Status,
                    Note = w.Note,
                })
                .ToListAsync(cancellationToken);

            return new WithdrawalReviewListResponse
            {
                Items = items,
                TotalCount = total,
                Page = request.Page,
                PageSize = request.PageSize,
            };
        }

        public async Task<WithdrawalReviewItemDto?> GetByIdAsync(
            int withdrawalId,
            CancellationToken cancellationToken = default
        )
        {
            return await _context
                .WithdrawalRequests.Include(w => w.Creator)
                .Where(w => w.WithdrawalId == withdrawalId)
                .Select(w => new WithdrawalReviewItemDto
                {
                    WithdrawalId = w.WithdrawalId,
                    CreatorId = w.CreatorId,
                    CreatorName = w.Creator.PenName,
                    AmountCoins = w.AmountCoins,
                    AmountVnd = w.AmountVnd,
                    BankAccountInfo = w.BankAccountInfo,
                    RequestedAt = w.RequestedAt,
                    ProcessedAt = w.ProcessedAt,
                    Status = w.Status,
                    Note = w.Note,
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<WithdrawalReviewItemDto> DecideAsync(
            int withdrawalId,
            WithdrawalDecisionRequest request,
            CancellationToken cancellationToken = default
        )
        {
            var entity =
                await _context
                    .WithdrawalRequests.Include(w => w.Creator)
                    .FirstOrDefaultAsync(w => w.WithdrawalId == withdrawalId, cancellationToken)
                ?? throw new KeyNotFoundException($"Withdrawal {withdrawalId} không tồn tại.");

            if (
                entity.Status == WithdrawalStatus.COMPLETED
                || entity.Status == WithdrawalStatus.REJECTED
            )
            {
                throw new InvalidOperationException("Yêu cầu đã được xử lý trước đó.");
            }

            if (request.Status == WithdrawalStatus.PENDING)
            {
                throw new InvalidOperationException("Không thể chuyển về trạng thái PENDING.");
            }

            entity.Status = request.Status;
            entity.ProcessedAt = DateTime.UtcNow;
            entity.Note = request.Note;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Withdrawal {WithdrawalId} updated to {Status} by admin.",
                withdrawalId,
                entity.Status
            );

            return new WithdrawalReviewItemDto
            {
                WithdrawalId = entity.WithdrawalId,
                CreatorId = entity.CreatorId,
                CreatorName = entity.Creator.PenName,
                AmountCoins = entity.AmountCoins,
                AmountVnd = entity.AmountVnd,
                BankAccountInfo = entity.BankAccountInfo,
                RequestedAt = entity.RequestedAt,
                ProcessedAt = entity.ProcessedAt,
                Status = entity.Status,
                Note = entity.Note,
            };
        }
    }
}
