using Application.DTOs.Community;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace mlndex_backend.Controllers.Moderation
{
    [ApiController]
    [Route("api/admin/comments")]
    [Authorize(Roles = "ADMIN,MODERATOR")]
    public class CommentModerationController : ControllerBase
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
            CancellationToken cancellationToken = default)
        {
            var response = await _commentModerationService.GetAdminCommentsAsync(search, status, page, pageSize, cancellationToken);
            return Ok(response);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCommentStatus(int id, [FromBody] UpdateCommentStatusRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var moderatorId))
            {
                return Unauthorized("User ID claim is missing or invalid.");
            }

            try
            {
                await _commentModerationService.UpdateStatusAsync(id, moderatorId, request.Action, cancellationToken);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật trạng thái bình luận." });
            }
        }

        [HttpPut("bulk")]
        public async Task<IActionResult> BulkUpdateCommentStatus([FromBody] BulkUpdateCommentStatusRequest request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdString, out var moderatorId))
            {
                return Unauthorized("User ID claim is missing or invalid.");
            }

            try
            {
                await _commentModerationService.BulkUpdateStatusAsync(request.CommentIds, moderatorId, request.Action, cancellationToken);
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật trạng thái các bình luận." });
            }
        }
    }
}
