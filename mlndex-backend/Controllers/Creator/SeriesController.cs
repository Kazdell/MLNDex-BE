using Application.DTOs.Creator;
using Application.Interfaces.Creator;
using Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace mlndex_backend.Controllers.Creator
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeriesController : BaseController
    {
        private readonly MlndexDbContext _context;
        private readonly ISeriesService _service;

        public SeriesController(MlndexDbContext context, ISeriesService service)
        {
            _context = context;
            _service = service;
        }

        private int CurrentUserId => GetUserId();

        [HttpGet("creator")]
        [Authorize(Roles = "CREATOR,ADMIN")]
        public async Task<IActionResult> GetSeriesByCreator(CancellationToken cancellationToken)
        {
            var creatorId = CurrentUserId;
            if (creatorId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

            // Tìm profile creator của user này
            var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == creatorId);
            if (creator == null) return NotFoundResponse("Không tìm thấy hồ sơ người sáng tạo.");

            var result = await _service.GetByCreatorAsync(creator.CreatorId, cancellationToken);
            return Ok(result);
        }

        [HttpPost("create")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "CREATOR,ADMIN")]
        public async Task<IActionResult> Create([FromForm] CreateSeriesDto dto, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

            var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
            if (creator == null) return NotFoundResponse("Không tìm thấy hồ sơ người sáng tạo.");

            var result = await _service.CreateAsync(userId, dto, cancellationToken);
            return Ok(result);
        }

        [HttpGet("{id}/edit")]
        [Authorize(Roles = "CREATOR,ADMIN")]
        public async Task<IActionResult> GetForEdit(int id)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

            var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
            if (creator == null) return NotFoundResponse("Không tìm thấy hồ sơ người sáng tạo.");

            var result = await _service.GetForEditAsync(id, userId);
            if (result == null)
                return NotFoundResponse("Không tìm thấy truyện hoặc bạn không có quyền chỉnh sửa.");
            return OkResponse(result);
        }

        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "CREATOR,ADMIN")]
        public async Task<IActionResult> Update(int id, [FromForm] CreateSeriesDto dto, CancellationToken cancellationToken)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

            var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
            if (creator == null) return NotFoundResponse("Không tìm thấy hồ sơ người sáng tạo.");

            var result = await _service.UpdateAsync(id, userId, dto, cancellationToken);
            return Ok(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "CREATOR,ADMIN")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = CurrentUserId;
            if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

            var creator = await _context.CreatorProfiles.FirstOrDefaultAsync(c => c.UserId == userId);
            if (creator == null) return NotFoundResponse("Không tìm thấy hồ sơ người sáng tạo.");

            await _service.DeleteAsync(id, userId);
            return NoContent();
        }

        [HttpGet]
        public async Task<IActionResult> GetSeries([FromQuery] string sortBy = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _service.GetSeriesListAsync(sortBy, page, pageSize);
            return OkResponse(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchSeries([FromQuery] SeriesSearchRequest request)
        {
            var result = await _service.SearchSeriesAsync(request);
            return OkResponse(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSeriesDetails(int id)
        {
            var result = await _service.GetSeriesDetailsAsync(id);
            if (result == null)
                return NotFoundResponse("Series not found.");

            return OkResponse(result);
        }

        [HttpGet("recommendations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRecommendations([FromQuery] int limit = 10)
        {
            var userId = CurrentUserId;
            var result = await _service.GetRecommendationsAsync(userId, limit);
            return OkResponse(result);
        }
    }
}
