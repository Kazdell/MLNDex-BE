using Application.DTOs.Common;
using Application.Exceptions;
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
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.WITHDRAWAL_NOT_FOUND, $"Withdrawal {withdrawalId} không tồn tại.");

      if (
          entity.Status == WithdrawalStatus.COMPLETED
          || entity.Status == WithdrawalStatus.REJECTED
      )
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Yêu cầu đã được xử lý trước đó.");
      }

      if (request.Status == WithdrawalStatus.PENDING)
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Không thể chuyển về trạng thái PENDING.");
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

    public async Task<WithdrawalReviewItemDto> RequestAsync(
        int creatorId,
        CreateWithdrawalRequestDto dto,
        CancellationToken cancellationToken = default
    )
    {
      var config = await _context.SystemConfigs.FirstOrDefaultAsync(cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Hệ thống chưa được cấu hình.");

      // 1. Validate limits
      if (dto.AmountCoins < config.WithdrawalMinCoins)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.INVALID_WITHDRAWAL_AMOUNT, $"Số tiền rút tối thiểu là {config.WithdrawalMinCoins} coins.");
      if (config.WithdrawalMaxCoins > 0 && dto.AmountCoins > config.WithdrawalMaxCoins)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.INVALID_WITHDRAWAL_AMOUNT, $"Số tiền rút tối đa là {config.WithdrawalMaxCoins} coins.");

      // 2. Check wallet balance
      var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == creatorId, cancellationToken)
          ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

      if (wallet.CoinBalance < dto.AmountCoins)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED, "Số dư không đủ để thực hiện yêu cầu này.");

      // 3. Calculate VND amount after fee
      var amountVnd = dto.AmountCoins * config.ExchangeRateCoinToVnd * (1 - config.WithdrawalFeePercent / 100);

      // 4. Create request
      var entity = new WithdrawalRequest
      {
        CreatorId = creatorId,
        AmountCoins = dto.AmountCoins,
        AmountVnd = amountVnd,
        BankAccountInfo = $"{dto.BankName} | {dto.AccountNumber} | {dto.AccountName}",
        RequestedAt = DateTime.UtcNow,
        Status = WithdrawalStatus.PENDING
      };

      // 5. Deduct coins from balance (or mark as pending - here we deduct immediately for simplicity)
      wallet.CoinBalance -= dto.AmountCoins;
      // Optionally add to total spent or similar? 

      _context.WithdrawalRequests.Add(entity);

      // Add a system transaction record
      _context.Transactions.Add(new Transaction
      {
        UserId = creatorId,
        WalletId = wallet.WalletId,
        Type = TransactionType.WITHDRAWAL,
        AmountCoins = dto.AmountCoins,
        Status = TransactionStatus.PENDING,
        Note = $"Yêu cầu rút {dto.AmountCoins} coins ({amountVnd:N0} VND)",
        CreatedAt = DateTime.UtcNow
      });

      await _context.SaveChangesAsync(cancellationToken);

      var creator = await _context.CreatorProfiles.FindAsync(creatorId);

      return new WithdrawalReviewItemDto
      {
        WithdrawalId = entity.WithdrawalId,
        CreatorId = entity.CreatorId,
        CreatorName = creator?.PenName ?? "Unknown",
        AmountCoins = entity.AmountCoins,
        AmountVnd = entity.AmountVnd,
        BankAccountInfo = entity.BankAccountInfo,
        RequestedAt = entity.RequestedAt,
        Status = entity.Status
      };
    }
  }
}
