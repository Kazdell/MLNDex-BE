using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace Application.DTOs.Financial
{
  public class WithdrawalReviewListRequest
  {
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 20;

    public WithdrawalStatus? Status { get; set; }

    public int? CreatorId { get; set; }
  }

  public class WithdrawalReviewItemDto
  {
    public int WithdrawalId { get; set; }
    public int CreatorId { get; set; }
    public string CreatorName { get; set; } = string.Empty;
    public decimal AmountCoins { get; set; }
    public decimal AmountVnd { get; set; }
    public string BankAccountInfo { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public WithdrawalStatus Status { get; set; }
    public string? Note { get; set; }
  }

  public class CreateWithdrawalRequestDto
  {
    public decimal AmountCoins { get; set; }
    public string BankName { get; set; } = null!;
    public string AccountNumber { get; set; } = null!;
    public string AccountName { get; set; } = null!;
  }

  public class WithdrawalReviewListResponse
  {
    public List<WithdrawalReviewItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
  }

  public class WithdrawalDecisionRequest
  {
    [Required]
    public WithdrawalStatus Status { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
  }
}
