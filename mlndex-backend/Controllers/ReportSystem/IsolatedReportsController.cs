using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;
using Application.Interfaces.ReportSystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IsolatedReportsController : BaseController
    {
        private readonly IPlagiarismReportService _reportService;
        private readonly IReputationService _reputationService;

        public IsolatedReportsController(
            IPlagiarismReportService reportService,
            IReputationService reputationService
        )
        {
            _reportService = reportService;
            _reputationService = reputationService;
        }

        private int GetCurrentUserId()
        {
            var str = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("UserId");
            return int.TryParse(str, out var id) ? id : 0;
        }

        // ══════════════════════════════════════════════════════
        // REPORT ENDPOINTS (existing)
        // ══════════════════════════════════════════════════════

        /// <summary>Tạo một báo cáo vi phạm mới (Người dùng).</summary>
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> CreateReport(
            [FromBody] CreatePlagiarismReportRequest request
        )
        {
            var userId = GetCurrentUserId();
            if (userId < 0)
                return Unauthorized("Phiên đăng nhập không hợp lệ.");

            try
            {
                var result = await _reportService.CreateReportAsync(userId, request);
                return OkResponse(result, "Đã gửi báo cáo thành công.");
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Lấy danh sách các báo cáo đang chờ xử lý (Moderator).</summary>
        [HttpGet("/api/isolated-moderator/reports")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> GetPendingReports(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20
        )
        {
            var reports = await _reportService.GetPendingReportsAsync(page, limit);
            return OkResponse(reports);
        }

        /// <summary>Lấy thống kê báo cáo (Moderator).</summary>
        [HttpGet("/api/isolated-moderator/reports/stats")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _reportService.GetReportStatsAsync();
            return OkResponse(stats);
        }

        /// <summary>Xử lý một báo cáo (Moderator).</summary>
        [HttpPost("/api/isolated-moderator/reports/{id}/resolve")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> ResolveReport(
            int id,
            [FromBody] ResolvePlagiarismReportRequest request
        )
        {
            var modId = GetCurrentUserId();
            if (modId == 0)
                return Unauthorized("Phiên đăng nhập không hợp lệ.");

            try
            {
                var result = await _reportService.ResolveReportAsync(id, modId, request);
                return OkResponse(result, "Đã xử lý báo cáo thành công.");
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>So sánh side-by-side (Moderator).</summary>
        [HttpGet("/api/isolated-moderator/reports/{id}/compare-data")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> GetCompareData(
            int id,
            [FromQuery] int referenceTranslationId
        )
        {
            try
            {
                var data = await _reportService.GetCompareDataAsync(id, referenceTranslationId);
                return OkResponse(data);
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════
        // REPUTATION ENDPOINTS (Phase A)
        // ══════════════════════════════════════════════════════

        /// <summary>Admin phục hồi điểm uy tín cho Creator/Team.</summary>
        [HttpPost("/api/isolated-moderator/reputation/restore")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> RestoreReputationScore(
            [FromBody] RestoreReputationRequest request
        )
        {
            var modId = GetCurrentUserId();
            if (modId == 0)
                return Unauthorized("Phiên đăng nhập không hợp lệ.");

            try
            {
                var result = await _reputationService.RestoreReputationScoreAsync(request, modId);
                return OkResponse(result, "Đã phục hồi điểm uy tín thành công.");
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Lấy danh sách overview uy tín của creator hoặc team.</summary>
        [HttpGet("/api/isolated-moderator/reputation/overview")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> GetReputationOverview(
            [FromQuery] string type = "creator",
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                var overview = await _reputationService.GetReputationOverviewAsync(type, search, page, pageSize);
                return OkResponse(overview);
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════
        // APPEAL ENDPOINTS (Phase C)
        // ══════════════════════════════════════════════════════

        /// <summary>User gửi đơn kháng cáo.</summary>
        [HttpPost("/api/appeals")]
        [Authorize]
        public async Task<IActionResult> CreateAppeal([FromBody] CreateAppealRequest request)
        {
            var userId = GetCurrentUserId();
            if (userId < 0)
                return Unauthorized("Phiên đăng nhập không hợp lệ.");

            try
            {
                var result = await _reputationService.CreateAppealAsync(userId, request);
                return OkResponse(result, "Đã gửi đơn kháng cáo thành công.");
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>Moderator xem danh sách kháng cáo chờ xử lý.</summary>
        [HttpGet("/api/isolated-moderator/appeals")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> GetPendingAppeals(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20
        )
        {
            var appeals = await _reputationService.GetPendingAppealsAsync(page, limit);
            return OkResponse(appeals);
        }

        /// <summary>Moderator duyệt/từ chối đơn kháng cáo.</summary>
        [HttpPost("/api/isolated-moderator/appeals/{id}/review")]
        [Authorize(Roles = "ADMIN,MODERATOR")]
        public async Task<IActionResult> ReviewAppeal(
            int id,
            [FromBody] ReviewAppealRequest request
        )
        {
            var modId = GetCurrentUserId();
            if (modId == 0)
                return Unauthorized("Phiên đăng nhập không hợp lệ.");

            try
            {
                var result = await _reputationService.ReviewAppealAsync(id, modId, request);
                return OkResponse(result, "Đã xử lý đơn kháng cáo.");
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════
        // REPUTATION HISTORY (Phase F)
        // ══════════════════════════════════════════════════════

        /// <summary>Lấy lịch sử uy tín của creator hoặc team.</summary>
        [HttpGet("/api/reputation/history")]
        [Authorize]
        public async Task<IActionResult> GetReputationHistory(
            [FromQuery] int? creatorId,
            [FromQuery] int? teamId,
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20)
        {
            if (!creatorId.HasValue && !teamId.HasValue)
                return BadRequestResponse("Cần cung cấp creatorId hoặc teamId.");

            try
            {
                var history = await _reputationService.GetReputationHistoryAsync(creatorId, teamId, page, limit);
                return OkResponse(history);
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════
        // TRANSLATION PORTFOLIO (Phase E)
        // ══════════════════════════════════════════════════════

        /// <summary>Lấy lịch sử tham gia dịch thuật của user.</summary>
        [HttpGet("/api/users/{userId}/translations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetUserTranslationHistory(int userId)
        {
            try
            {
                var history = await _reputationService.GetUserTranslationHistoryAsync(userId);
                return OkResponse(history);
            }
            catch (System.Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }
    }
}
