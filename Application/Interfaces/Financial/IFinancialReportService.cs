using Application.DTOs.Financial;

namespace Application.Interfaces.Financial
{
  public interface IFinancialReportService
  {
    Task<FinancialReportResponse> GetSummaryAsync(
        FinancialReportRequest request,
        CancellationToken cancellationToken = default
    );
  }
}
