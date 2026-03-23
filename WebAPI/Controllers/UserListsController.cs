using Application.DTOs.Common;
using Application.DTOs.UserList;
using Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using WebAPI.Controllers.Base;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserListsController : BaseController
    {
        private readonly IUserListService _userListService;

        public UserListsController(IUserListService userListService)
        {
            _userListService = userListService;
        }

        [HttpGet("public")]
        public async Task<IActionResult> GetPublicLists([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _userListService.GetPublicListsAsync(page, pageSize);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetMyLists([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            var result = await _userListService.GetUserListsAsync(userId, page, pageSize);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetListDetail(int id)
        {
            int? currentUserId = null;
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                currentUserId = GetUserId();
            }

            var result = await _userListService.GetListDetailAsync(id, currentUserId);
            return Ok(ApiResponse<object>.SuccessResponse(result));
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateList([FromBody] CreateUserListDto request)
        {
            var userId = GetUserId();
            var result = await _userListService.CreateListAsync(userId, request);
            return Ok(ApiResponse<object>.SuccessResponse(result, "Tạo danh sách thành công"));
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateList(int id, [FromBody] UpdateUserListDto request)
        {
            var userId = GetUserId();
            var result = await _userListService.UpdateListAsync(userId, id, request);
            return Ok(ApiResponse<object>.SuccessResponse(result, "Cập nhật danh sách thành công"));
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteList(int id)
        {
            var userId = GetUserId();
            await _userListService.DeleteListAsync(userId, id);
            return Ok(ApiResponse<object>.SuccessResponse(null, "Xóa danh sách thành công"));
        }

        [Authorize]
        [HttpPost("{id}/items")]
        public async Task<IActionResult> AddItemToList(int id, [FromBody] AddUserListItemDto request)
        {
            var userId = GetUserId();
            var result = await _userListService.AddItemToListAsync(userId, id, request);
            return Ok(ApiResponse<object>.SuccessResponse(result, "Đã thêm truyện vào danh sách"));
        }

        [Authorize]
        [HttpDelete("{id}/items/{seriesId}")]
        public async Task<IActionResult> RemoveItemFromList(int id, int seriesId)
        {
            var userId = GetUserId();
            await _userListService.RemoveItemFromListAsync(userId, id, seriesId);
            return Ok(ApiResponse<object>.SuccessResponse(null, "Đã xóa truyện khỏi danh sách"));
        }
    }
}
