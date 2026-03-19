using Application.DTOs.User;
using Application.Interfaces.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace mlndex_backend.Controllers.User
{
    [ApiController]
    [Route("api/user/follows")]
    [Authorize]
    public class FollowController : BaseController
    {
        private readonly IFollowService _followService;
        private int CurrentUserId => GetUserId();

        public FollowController(IFollowService followService)
        {
            _followService = followService;
        }

        /// <summary>
        /// Follow a series/creator/team
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Follow([FromBody] FollowRequestDto dto, CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            try
            {
                var result = await _followService.FollowAsync(userId, dto, ct);
                return OkResponse(result, "Followed successfully.");
            }
            catch (InvalidOperationException ex)
            {
                return ConflictResponse(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>
        /// Unfollow a target
        /// </summary>
        [HttpDelete("{targetId}")]
        public async Task<IActionResult> Unfollow(int targetId, [FromQuery] string type = "SERIES", CancellationToken ct = default)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _followService.UnfollowAsync(userId, targetId, type, ct);
            if (!result) return NotFoundResponse("Follow not found.");

            return OkResponse(true, "Unfollowed successfully.");
        }

        /// <summary>
        /// Get all followed series
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetFollowedSeries(CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _followService.GetFollowedSeriesAsync(userId, ct);
            return OkResponse(result);
        }

        /// <summary>
        /// Check if user is following a target
        /// </summary>
        [HttpGet("check/{targetId}")]
        public async Task<IActionResult> CheckFollowStatus(int targetId, [FromQuery] string type = "SERIES", CancellationToken ct = default)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _followService.CheckFollowStatusAsync(userId, targetId, type, ct);
            return OkResponse(result);
        }

        /// <summary>
        /// Get follow count for a target (public, no auth needed)
        /// </summary>
        [HttpGet("count/{targetId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetFollowCount(int targetId, [FromQuery] string type = "SERIES", CancellationToken ct = default)
        {
            var count = await _followService.GetFollowCountAsync(targetId, type, ct);
            return OkResponse(new { count });
        }
    }
}
