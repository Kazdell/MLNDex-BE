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
    [Route("api/user/ratings")]
    [Authorize]
    public class RatingController : BaseController
    {
        private readonly IRatingService _ratingService;
        private int CurrentUserId => GetUserId();

        public RatingController(IRatingService ratingService)
        {
            _ratingService = ratingService;
        }

        /// <summary>
        /// Create or update a rating (upsert)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpsertRating([FromBody] RatingRequestDto dto, CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            try
            {
                var result = await _ratingService.UpsertRatingAsync(userId, dto, ct);
                return OkResponse(result, "Rating saved successfully.");
            }
            catch (ArgumentException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        /// <summary>
        /// Get user's rating for a specific series
        /// </summary>
        [HttpGet("{seriesId}")]
        public async Task<IActionResult> GetUserRating(int seriesId, CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _ratingService.GetUserRatingAsync(userId, seriesId, ct);
            if (result == null) return NotFoundResponse("No rating found.");

            return OkResponse(result);
        }

        /// <summary>
        /// Delete user's rating
        /// </summary>
        [HttpDelete("{seriesId}")]
        public async Task<IActionResult> DeleteRating(int seriesId, CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _ratingService.DeleteRatingAsync(userId, seriesId, ct);
            if (!result) return NotFoundResponse("Rating not found.");

            return OkResponse(true, "Rating deleted successfully.");
        }

        /// <summary>
        /// Get rating summary for a series (public)
        /// </summary>
        [HttpGet("summary/{seriesId}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSeriesRatingSummary(int seriesId, CancellationToken ct)
        {
            // Try to get userId for personalized response
            var userId = GetUserId();
            int? nullableUserId = userId > 0 ? userId : null;

            var result = await _ratingService.GetSeriesRatingSummaryAsync(seriesId, nullableUserId, ct);
            return OkResponse(result);
        }
    }
}
