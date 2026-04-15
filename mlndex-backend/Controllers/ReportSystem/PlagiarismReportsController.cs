using System.Security.Claims;
using Application.DTOs.Common;
using Application.DTOs.ReportSystem;
using Application.Exceptions;
using Application.Interfaces.ReportSystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.ReportSystem
{
    [ApiController]
    [Route("api/v1/plagiarism-reports")]
    public class PlagiarismReportsController : BaseController
    {
        private readonly IPlagiarismReportService _reportService;

        public PlagiarismReportsController(IPlagiarismReportService reportService)
        {
            _reportService = reportService;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReport(
            [FromBody] CreatePlagiarismReportRequest request
        )
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                throw new AppException(ErrorCodes.UNAUTHORIZED);

            var result = await _reportService.CreateReportAsync(userId, request);
            return OkResponse(result);
        }

        [HttpGet("moderator")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> GetPendingReports(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20
        )
        {
            var result = await _reportService.GetPendingReportsAsync(page, limit);
            return OkResponse(result);
        }

        [HttpPost("moderator/{id}/resolve")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> ResolveReport(
            int id,
            [FromBody] ResolvePlagiarismReportRequest request
        )
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out var userId))
                throw new AppException(ErrorCodes.UNAUTHORIZED);

            try
            {
                var result = await _reportService.ResolveReportAsync(id, userId, request);
                return OkResponse(result);
            }
            catch (InvalidOperationException ex)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
            }
        }

        [HttpGet("moderator/{id}/compare")]
        [Authorize(Roles = "Admin,Moderator")]
        public async Task<IActionResult> GetCompareData(
            int id,
            [FromQuery] int referenceTranslationId
        )
        {
            try
            {
                var result = await _reportService.GetCompareDataAsync(id, referenceTranslationId);
                return OkResponse(result);
            }
            catch (InvalidOperationException ex)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
            }
        }
    }
}
