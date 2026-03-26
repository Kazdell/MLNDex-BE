using Application.DTOs.Financial;

namespace Application.Interfaces.Financial
{
  public interface IWithdrawalService
  {
    Task<WithdrawalReviewListResponse> GetPendingAsync(
        WithdrawalReviewListRequest request,
        CancellationToken cancellationToken = default
    );
    Task<WithdrawalReviewItemDto?> GetByIdAsync(
        int withdrawalId,
        CancellationToken cancellationToken = default
    );
    Task<WithdrawalReviewItemDto> DecideAsync(
        int withdrawalId,
        WithdrawalDecisionRequest request,
        CancellationToken cancellationToken = default
    );
    Task<WithdrawalReviewItemDto> RequestAsync(
        int creatorId, 
        CreateWithdrawalRequestDto dto, 
        CancellationToken cancellationToken = default
    );
  }
}
