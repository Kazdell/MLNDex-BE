using Application.DTOs.Creator;
using Application.Interfaces.Creator;
using Infrastructure.Persistence.Data;
using Microsoft.AspNetCore.Mvc;


namespace mlndex_backend.Controllers.Creator
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeriesController : BaseController
    {
        private readonly MlndexDbContext _context;
        private readonly INovelService _service;

        public SeriesController(MlndexDbContext context, INovelService service)
        {
            _context = context;
            _service = service;
        }

        [HttpPost("create")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create(
            [FromForm] CreateSeriesDto dto,
            CancellationToken cancellationToken)
        {
            // TODO: Replace hardcode with JWT claim when auth is ready
            // var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
            // var creatorId = await _creatorService.GetCreatorIdByUserId(userId);
            var creatorId = 1;

            var result = await _service.CreateAsync(creatorId, dto, cancellationToken);
            return Ok(result);
        }

    }
}
