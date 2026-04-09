using Microsoft.Extensions.Caching.Memory;
using Application.DTOs.Creator;
using Application.Interfaces.Creator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System;
using Infrastructure.Data;

namespace mlndex_backend.Controllers.Creator
{
  [Route("api/[controller]")]
  [ApiController]
  public class SeriesController : BaseController
  {
    private readonly MlndexDbContext _context;
    private readonly ISeriesService _service;
    private readonly Application.Interfaces.Creator.IRecommendationService _recommendationService;
    private readonly IMemoryCache _cache;

    public SeriesController(MlndexDbContext context, ISeriesService service, Application.Interfaces.Creator.IRecommendationService recommendationService, IMemoryCache cache)
    {
      _context = context;
      _service = service;
      _recommendationService = recommendationService;
      _cache = cache;
    }

    private int CurrentUserId => GetUserId();

    [HttpGet("creator")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> GetSeriesByCreator(CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");
      var result = await _service.GetByCreatorAsync(userId, cancellationToken);
      return Ok(result);
    }

    [HttpPost("create")]
    [Consumes("multipart/form-data")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> Create([FromForm] CreateSeriesDto dto, CancellationToken cancellationToken)
    {
      var userId = CurrentUserId;
      if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");

      var currentUser = await _context.Users.FindAsync(userId);
      if (currentUser?.CannotUpload == true)
        return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

      var result = await _service.CreateAsync(userId, dto, cancellationToken);
      return Ok(result);
    }

    [HttpGet("{id}/edit")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> GetForEdit(int id)
    {
      var userId = CurrentUserId;
      if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");
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

      var currentUser = await _context.Users.FindAsync(userId);
      if (currentUser?.CannotUpload == true)
        return StatusCode(403, new { message = "Tài khoản bị khoá chức năng upload do vi phạm nội quy. Vui lòng liên hệ mod để kháng cáo." });

      var result = await _service.UpdateAsync(id, userId, dto, cancellationToken);
      return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> Delete(int id)
    {
      var userId = CurrentUserId;
      if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");
      await _service.DeleteAsync(id, userId);
      return NoContent();
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "CREATOR,ADMIN")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateSeriesStatusRequest request, CancellationToken ct)
    {
      var userId = CurrentUserId;
      if (userId == 0) return UnauthorizedResponse("Không tìm thấy thông tin định danh người dùng.");
      try
      {
        await _service.UpdateStatusAsync(id, userId, request.Status, ct);
        return OkResponse<object?>(null, "Cập nhật trạng thái thành công.");
      }
      catch (Exception ex)
      {
        return ErrorResponse(ex.Message);
      }
    }

    [HttpGet]
    public async Task<IActionResult> GetSeries([FromQuery] string sortBy = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
      // Only cache the first few pages of newest and popular which are heavily hit by the landing page
      if ((sortBy == "newest" || sortBy == "popular") && page <= 2)
      {
        var cacheKey = $"GetSeries_{sortBy}_{page}_{pageSize}";
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
          return OkResponse(cachedResult);
        }

        var result = await _service.GetSeriesListAsync(sortBy, page, pageSize);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(2));

        _cache.Set(cacheKey, result, cacheEntryOptions);
        return OkResponse(result);
      }

      var uncachedResult = await _service.GetSeriesListAsync(sortBy, page, pageSize);
      return OkResponse(uncachedResult);
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
      int? userId = null;
      if (User.Identity?.IsAuthenticated == true)
      {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (int.TryParse(claim, out var parsedId)) userId = parsedId;
      }

      var result = await _service.GetSeriesDetailsAsync(id, userId);
      if (result == null)
        return NotFoundResponse("Series not found.");

      return OkResponse(result);
    }

    [HttpGet("recommendations")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecommendations([FromQuery] int limit = 10, [FromQuery] int? currentSeriesId = null)
    {
      var userId = CurrentUserId;

      // If user is not logged in, cache the generalized recommendations
      if (userId == 0)
      {
        var cacheKey = $"GetRecommendations_Anon_{limit}_{currentSeriesId}";
        if (_cache.TryGetValue(cacheKey, out var cachedResult))
        {
          return OkResponse(cachedResult);
        }

        var result = await _recommendationService.GetRecommendationsAsync(0, limit, currentSeriesId);

        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(10));

        _cache.Set(cacheKey, result, cacheEntryOptions);
        return OkResponse(result);
      }
      else
      {
        var result = await _recommendationService.GetRecommendationsAsync(userId, limit, currentSeriesId);
        return OkResponse(result);
      }
    }
  }
}
