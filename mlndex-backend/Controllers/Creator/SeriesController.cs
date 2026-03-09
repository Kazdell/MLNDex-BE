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
    private int CurrentUserId => int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

    public SeriesController(MlndexDbContext context, ISeriesService service)
    {
      _context = context;
      _service = service;
    }

    [HttpGet]
    [Route("creator")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> GetSeriesByCreator(
        CancellationToken cancellationToken)
    {
      var creatorId = CurrentUserId;
      if (creatorId == 0) return UnauthorizedResponse();

      var result = await _service.GetByCreatorAsync(creatorId, cancellationToken);
      return Ok(result);
    }


    [HttpPost("create")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> Create(
        [FromForm] CreateSeriesDto dto,
        CancellationToken cancellationToken)
    {
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");
      
      var creatorId = int.Parse(userIdClaim.Value); // Map UserId to CreatorId simplified for now

      var result = await _service.CreateAsync(creatorId, dto, cancellationToken);
      return Ok(result);
    }

    [HttpGet("{id}/edit")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> GetForEdit(int id)
    {
      var creatorId = CurrentUserId;
      if (creatorId == 0) return UnauthorizedResponse();

      var result = await _service.GetForEditAsync(id, creatorId);
      if (result == null)
        return NotFoundResponse("Không tìm thấy truyện hoặc bạn không có quyền chỉnh sửa.");
      return OkResponse(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
      var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
      if (userIdClaim == null) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");
      
      var creatorId = int.Parse(userIdClaim.Value); // Map UserId to CreatorId simplified for now
      await _service.DeleteAsync(id, creatorId);
      return NoContent();
    }

    [HttpPut("{id}")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> Update(
        int id,
        [FromForm] CreateSeriesDto dto,
        CancellationToken cancellationToken)
    {
      var creatorId = CurrentUserId;
      if (creatorId == 0) return UnauthorizedResponse();

      var result = await _service.UpdateAsync(id, creatorId, dto, cancellationToken);
      return Ok(result);
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
    public async Task<IActionResult> GetRecommendations([FromQuery] int limit = 10)
    {
      var currentUserId = CurrentUserId;
      // If not logged in, currentUserId = 0, service should handle guest recommendations
      var result = await _service.GetRecommendationsAsync(currentUserId, limit);
      return OkResponse(result);
    }
  }
}
