using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Common;
using Application.DTOs.ReportSystem;

namespace Application.Interfaces.ReportSystem
{
  public interface IPlagiarismReportService
  {
    Task<PlagiarismReportDto> CreateReportAsync(int reporterId, CreatePlagiarismReportRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<PlagiarismReportDto>> GetPendingReportsAsync(int page = 1, int limit = 20, CancellationToken cancellationToken = default);
    Task<PlagiarismReportStatsDto> GetReportStatsAsync(CancellationToken cancellationToken = default);
    Task<PlagiarismReportDto> ResolveReportAsync(int reportId, int moderatorId, ResolvePlagiarismReportRequest request, CancellationToken cancellationToken = default);
    Task<CompareTranslationResponse> GetCompareDataAsync(int reportId, int referenceTranslationId, CancellationToken cancellationToken = default);
  }
}
