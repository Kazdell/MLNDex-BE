using System.Security.Claims;
using Application.DTOs.Common;
using Application.DTOs.Community;
using Application.Exceptions;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Moderation
{
    [ApiController]
    [Route("api/admin/comments")]
    [Authorize(Roles = "ADMIN,MODERATOR")]
    public class CommentModerationController : BaseController
    {
        private readonly ICommentModerationService _commentModerationService;

        public CommentModerationController(ICommentModerationService commentModerationService)
        {
            _commentModerationService = commentModerationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAdminComments(
            [FromQuery] string? search,
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            CancellationToken cancellationToken = default
        )
        {
            var response = await _commentModerationService.GetAdminCommentsAsync(
                search,
                status,
                page,
                pageSize,
                cancellationToken
            );
            return OkResponse(response);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetAdminStats(CancellationToken cancellationToken = default)
        {
            var response = await _commentModerationService.GetAdminStatsAsync(cancellationToken);
            return OkResponse(response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCommentStatus(
            int id,
            [FromBody] UpdateCommentStatusRequest request,
            CancellationToken cancellationToken
        )
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var moderatorId))
            {
                throw new AppException(ErrorCodes.UNAUTHORIZED);
            }

            try
            {
                await _commentModerationService.UpdateStatusAsync(
                    id,
                    moderatorId,
                    request.Action,
                    cancellationToken
                );
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        message = "Lỗi hệ thống khi cập nhật trạng thái bình luận.",
                        details = ex.Message,
                    }
                );
            }
        }

        [HttpPut("bulk")]
        public async Task<IActionResult> BulkUpdateCommentStatus(
            [FromBody] BulkUpdateCommentStatusRequest request,
            CancellationToken cancellationToken
        )
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var moderatorId))
            {
                throw new AppException(ErrorCodes.UNAUTHORIZED);
            }

            try
            {
                await _commentModerationService.BulkUpdateStatusAsync(
                    request.CommentIds,
                    moderatorId,
                    request.Action,
                    cancellationToken
                );
                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(
                    500,
                    new
                    {
                        message = "Lỗi hệ thống khi cập nhật trạng thái các bình luận.",
                        details = ex.Message,
                    }
                );
            }
        }
    }
}
