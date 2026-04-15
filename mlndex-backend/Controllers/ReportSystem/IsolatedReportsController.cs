using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Application.DTOs.Common;
using Application.DTOs.ReportSystem;
using Application.Exceptions;
using Application.Interfaces.Data;
using Application.Interfaces.ReportSystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace mlndex_backend.Controllers.ReportSystem
{
    [ApiController]
    [Route("api/[controller]")]
    public class IsolatedReportsController : BaseController
    {
        private readonly IMlndexDbContext _db;
        private readonly IPlagiarismReportService _reportService;
        private readonly ITrustScoreService _trustScoreService;

        public IsolatedReportsController(
            IMlndexDbContext db,
            IPlagiarismReportService reportService,
            ITrustScoreService trustScoreService
        )
        {
            _db = db;
            _reportService = reportService;
            _trustScoreService = trustScoreService;
        }

        private async Task<int> GetCurrentUserIdAsync()
        {
            var str =
                User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue("sub")
                ?? User.FindFirstValue("userId")
                ?? User.FindFirstValue("id");

            if (int.TryParse(str, out var id) && id > 0)
                return id;

            var username = User.FindFirstValue(ClaimTypes.Name);
            var email = User.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrWhiteSpace(username) || !string.IsNullOrWhiteSpace(email))
            {
                var normalizedUsername = username?.Trim().ToLower();
                var normalizedEmail = email?.Trim().ToLower();

                var userId = await _db
                    .Users.Where(u =>
                        (
                            !string.IsNullOrWhiteSpace(normalizedUsername)
                            && u.Username == normalizedUsername
                        )
                        || (
                            !string.IsNullOrWhiteSpace(normalizedEmail)
                            && u.Email == normalizedEmail
                        )
                    )
                    .Select(u => u.UserId)
                    .FirstOrDefaultAsync();

                if (userId > 0)
                    return userId;
            }

            if (User.IsInRole("ADMIN") || User.IsInRole("MODERATOR"))
            {
                var fallbackStaffId = await _db
                    .UserRoles.Include(ur => ur.Role)
                    .Where(ur =>
                        ur.UserId > 0
                        && (
                            ur.Role.RoleName.ToString() == "ADMIN"
                            || ur.Role.RoleName.ToString() == "MODERATOR"
                        )
                    )
                    .Select(ur => ur.UserId)
                    .FirstOrDefaultAsync();

                if (fallbackStaffId > 0)
                    return fallbackStaffId;
            }

            return 0;
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
            var userId = await GetCurrentUserIdAsync();
            if (userId == 0)
                throw new AppException(ErrorCodes.UNAUTHORIZED);

            try
            {
                var result = await _reportService.CreateReportAsync(userId, request);
                return OkResponse(result);
            }
            catch (System.Exception)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
            }
        }

        /// <summary>Lấy danh sách các báo cáo đang chờ xử lý (Moderator).</summary>
        [HttpGet("/api/isolated-moderator/reports")]
        [Authorize(Roles = "MODERATOR,ADMIN")]
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
        [Authorize(Roles = "MODERATOR,ADMIN")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _reportService.GetReportStatsAsync();
            return OkResponse(stats);
        }

        /// <summary>Xử lý một báo cáo (Moderator).</summary>
        [HttpPost("/api/isolated-moderator/reports/{id}/resolve")]
        [Authorize(Roles = "MODERATOR,ADMIN")]
        public async Task<IActionResult> ResolveReport(
            int id,
            [FromBody] ResolvePlagiarismReportRequest request
        )
        {
            var modId = await GetCurrentUserIdAsync();
            if (modId == 0)
                throw new AppException(ErrorCodes.UNAUTHORIZED);

            try
            {
                var result = await _reportService.ResolveReportAsync(id, modId, request);
                return OkResponse(result);
            }
            catch (System.Exception)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
            }
        }

        /// <summary>So sánh side-by-side (Moderator).</summary>
        [HttpGet("/api/isolated-moderator/reports/{id}/compare-data")]
        [Authorize(Roles = "MODERATOR,ADMIN")]
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
            catch (System.Exception)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
            }
        }

        // ══════════════════════════════════════════════════════
        // TRUST SCORE ENDPOINTS (Phase A)
        // ══════════════════════════════════════════════════════

        /// <summary>Admin phục hồi điểm uy tín cho User/Team.</summary>
        [HttpPost("/api/isolated-moderator/trust-score/restore")]
        [Authorize(Roles = "MODERATOR,ADMIN")]
        public async Task<IActionResult> RestoreTrustScore(
            [FromBody] RestoreTrustScoreRequest request
        )
        {
            var modId = await GetCurrentUserIdAsync();
            if (modId == 0)
                throw new AppException(ErrorCodes.UNAUTHORIZED);

            try
            {
                var result = await _trustScoreService.RestoreTrustScoreAsync(request, modId);
                return OkResponse(result);
            }
            catch (System.Exception)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
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
            var userId = await GetCurrentUserIdAsync();
            if (userId == 0)
                throw new AppException(ErrorCodes.UNAUTHORIZED);

            try
            {
                var result = await _trustScoreService.CreateAppealAsync(userId, request);
                return OkResponse(result);
            }
            catch (System.Exception)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
            }
        }

        /// <summary>Moderator xem danh sách kháng cáo chờ xử lý.</summary>
        [HttpGet("/api/isolated-moderator/appeals")]
        [Authorize(Roles = "MODERATOR,ADMIN")]
        public async Task<IActionResult> GetPendingAppeals(
            [FromQuery] int page = 1,
            [FromQuery] int limit = 20
        )
        {
            var appeals = await _trustScoreService.GetPendingAppealsAsync(page, limit);
            return OkResponse(appeals);
        }

        /// <summary>Moderator duyệt/từ chối đơn kháng cáo.</summary>
        [HttpPost("/api/isolated-moderator/appeals/{id}/review")]
        [Authorize(Roles = "MODERATOR,ADMIN")]
        public async Task<IActionResult> ReviewAppeal(
            int id,
            [FromBody] ReviewAppealRequest request
        )
        {
            var modId = await GetCurrentUserIdAsync();
            if (modId == 0)
                throw new AppException(ErrorCodes.UNAUTHORIZED);

            try
            {
                var result = await _trustScoreService.ReviewAppealAsync(id, modId, request);
                return OkResponse(result);
            }
            catch (System.Exception)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
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
                var history = await _trustScoreService.GetUserTranslationHistoryAsync(userId);
                return OkResponse(history);
            }
            catch (System.Exception)
            {
                throw new AppException(ErrorCodes.INVALID_INPUT);
            }
        }
    }
}
