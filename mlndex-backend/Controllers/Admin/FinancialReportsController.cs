using Application.DTOs.Financial;
using Application.Interfaces.Financial;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Admin
{
    [Route("api/admin/financial-reports")]
    public class FinancialReportsController : BaseController
    {
        private readonly IFinancialReportService _service;

        public FinancialReportsController(IFinancialReportService service)
        {
            _service = service;
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] FinancialReportRequest request,
            CancellationToken cancellationToken
        )
        {
            var result = await _service.GetSummaryAsync(request, cancellationToken);
            return OkResponse(result);
        }
    }
}
