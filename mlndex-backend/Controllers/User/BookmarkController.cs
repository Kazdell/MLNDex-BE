using Application.DTOs.User;
using Application.Interfaces.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace mlndex_backend.Controllers.User
{
    [ApiController]
    [Route("api/user/bookmarks")]
    [Authorize]
    public class BookmarkController : BaseController
    {
        private readonly IBookmarkService _bookmarkService;
        private int CurrentUserId => GetUserId();

        public BookmarkController(IBookmarkService bookmarkService)
        {
            _bookmarkService = bookmarkService;
        }

        /// <summary>
        /// Save/update bookmark position
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> UpsertBookmark([FromBody] BookmarkRequestDto dto, CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _bookmarkService.UpsertBookmarkAsync(userId, dto, ct);
            return OkResponse(result, "Bookmark saved successfully.");
        }

        /// <summary>
        /// Get all user bookmarks
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetUserBookmarks(CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _bookmarkService.GetUserBookmarksAsync(userId, ct);
            return OkResponse(result);
        }

        /// <summary>
        /// Get bookmark for a specific series
        /// </summary>
        [HttpGet("{seriesId}")]
        public async Task<IActionResult> GetBookmarkForSeries(int seriesId, CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _bookmarkService.GetBookmarkForSeriesAsync(userId, seriesId, ct);
            // Return Ok even if null to prevent console 404 errors for unbookmarked series
            return OkResponse(result);
        }

        /// <summary>
        /// Delete a bookmark
        /// </summary>
        [HttpDelete("{bookmarkId}")]
        public async Task<IActionResult> DeleteBookmark(int bookmarkId, CancellationToken ct)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Invalid user context");

            var result = await _bookmarkService.DeleteBookmarkAsync(userId, bookmarkId, ct);
            if (!result) return NotFoundResponse("Bookmark not found.");

            return OkResponse(true, "Bookmark deleted successfully.");
        }
    }
}
