using Application.DTOs.Series;
using Application.Interfaces.Series;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;
using System.Threading.Tasks;

namespace mlndex_backend.Controllers.Series
{
    [ApiController]
    [Route("api/series")]
    public class SeriesController : BaseController
    {
        private readonly ISeriesService _seriesService;

        public SeriesController(ISeriesService seriesService)
        {
            _seriesService = seriesService;
        }

        [HttpGet]
        public async Task<IActionResult> GetSeries([FromQuery] string sortBy = "newest", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var result = await _seriesService.GetSeriesListAsync(sortBy, page, pageSize);
            return OkResponse(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchSeries([FromQuery] SeriesSearchRequest request)
        {
            var result = await _seriesService.SearchSeriesAsync(request);
            return OkResponse(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetSeriesDetails(int id)
        {
            var result = await _seriesService.GetSeriesDetailsAsync(id);
            if (result == null)
                return NotFoundResponse("Series not found.");
            
            return OkResponse(result);
        }

        [HttpGet("recommendations")]
        public async Task<IActionResult> GetRecommendations([FromQuery] int limit = 10)
        {
            // TODO: Extract UserId from Claims if using JWT Auth. 
            // Currently using default UserId = 1 for mock
            int currentUserId = 1; 
            var result = await _seriesService.GetRecommendationsAsync(currentUserId, limit);
            return OkResponse(result);
        }
    }
}
