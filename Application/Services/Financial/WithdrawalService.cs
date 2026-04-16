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
      var query = _context.WithdrawalRequests.Include(w => w.User).AsQueryable();

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

      if (request.UserId.HasValue)
      {
        query = query.Where(w => w.UserId == request.UserId.Value);
      }

      var total = await query.CountAsync(cancellationToken);

      var items = await query
          .OrderByDescending(w => w.RequestedAt)
          .Skip((request.Page - 1) * request.PageSize)
          .Take(request.PageSize)
          .Select(w => new WithdrawalReviewItemDto
          {
            WithdrawalId = w.WithdrawalId,
            UserId = w.UserId,
            UserName = w.User.DisplayName,
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
          .WithdrawalRequests.Include(w => w.User)
          .Where(w => w.WithdrawalId == withdrawalId)
          .Select(w => new WithdrawalReviewItemDto
          {
            WithdrawalId = w.WithdrawalId,
            UserId = w.UserId,
            UserName = w.User.DisplayName,
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
              .WithdrawalRequests.Include(w => w.User)
              .FirstOrDefaultAsync(w => w.WithdrawalId == withdrawalId, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.WITHDRAWAL_NOT_FOUND);

      if (
          entity.Status == WithdrawalStatus.COMPLETED
          || entity.Status == WithdrawalStatus.REJECTED
      )
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      }

      if (request.Status == WithdrawalStatus.PENDING)
      {
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);
      }

      entity.Status = request.Status;
      entity.ProcessedAt = DateTime.UtcNow;

      if (!string.IsNullOrWhiteSpace(request.Note))
      {
        if (string.IsNullOrWhiteSpace(entity.Note))
        {
          entity.Note = request.Note;
        }
        else
        {
          entity.Note = $"{entity.Note}\nAdmin: {request.Note}";
        }
      }

      await _context.SaveChangesAsync(cancellationToken);

      _logger.LogInformation(
          "Withdrawal {WithdrawalId} updated to {Status} by admin.",
          withdrawalId,
          entity.Status
      );

      return new WithdrawalReviewItemDto
      {
        WithdrawalId = entity.WithdrawalId,
        UserId = entity.UserId,
        UserName = entity.User.DisplayName,
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
        int userId,
        CreateWithdrawalRequestDto dto,
        CancellationToken cancellationToken = default
    )
    {
      var config = await _context.SystemConfigs.FirstOrDefaultAsync(cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // 1. Validate limits
      if (dto.AmountCoins < config.WithdrawalMinCoins)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.INVALID_WITHDRAWAL_AMOUNT);
      if (config.WithdrawalMaxCoins > 0 && dto.AmountCoins > config.WithdrawalMaxCoins)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.INVALID_WITHDRAWAL_AMOUNT);

      // 2. Check wallet balance
      var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId, cancellationToken)
          ?? throw new AppException(ErrorCodes.WALLET_NOT_FOUND);

      if (wallet.CoinBalance < dto.AmountCoins)
        throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      // 3. Calculate VND amount after fee
      var amountVnd = dto.AmountCoins * config.ExchangeRateCoinToVnd * (1 - config.WithdrawalFeePercent / 100);

      // 4. Create request
      var entity = new WithdrawalRequest
      {
        UserId = userId,
        AmountCoins = dto.AmountCoins,
        AmountVnd = amountVnd,
        BankAccountInfo = $"{dto.BankName} | {dto.AccountNumber} | {dto.AccountName}",
        RequestedAt = DateTime.UtcNow,
        Status = WithdrawalStatus.PENDING,
        Note = dto.Note
      };

      // 5. Deduct coins from balance (or mark as pending - here we deduct immediately for simplicity)
      wallet.CoinBalance -= dto.AmountCoins;
      // Optionally add to total spent or similar? 

      _context.WithdrawalRequests.Add(entity);

      // Add a system transaction record
      _context.Transactions.Add(new Transaction
      {
        UserId = userId,
        WalletId = wallet.WalletId,
        Type = TransactionType.WITHDRAWAL,
        AmountCoins = dto.AmountCoins,
        Status = TransactionStatus.PENDING,
        Note = $"Yêu cầu rút {dto.AmountCoins} coins ({amountVnd:N0} VND)",
        CreatedAt = DateTime.UtcNow
      });

      await _context.SaveChangesAsync(cancellationToken);

      var userProfile = await _context.Users.FindAsync(userId);

      return new WithdrawalReviewItemDto
      {
        WithdrawalId = entity.WithdrawalId,
        UserId = entity.UserId,
        UserName = userProfile?.DisplayName ?? "Unknown",
        AmountCoins = entity.AmountCoins,
        AmountVnd = entity.AmountVnd,
        BankAccountInfo = entity.BankAccountInfo,
        RequestedAt = entity.RequestedAt,
        Status = entity.Status
      };
    }
  }
}
