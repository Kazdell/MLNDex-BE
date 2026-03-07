using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.User
{
    [Route("api/reports")]
    public class ReportsController : BaseController
    {
        private readonly IReportService _service;

        public ReportsController(IReportService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] CreateReportRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            // TODO: replace with real reporterId from auth claims
            var reporterId = 1;

            var result = await _service.CreateAsync(reporterId, request, cancellationToken);
            return OkResponse(result, "Report submitted");
        }
    }
}
