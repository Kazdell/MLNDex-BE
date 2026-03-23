using System.Security.Claims;
using Application.DTOs.ReportSystem;
using Application.Interfaces.ReportSystem;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.ReportSystem
{
  [ApiController]
  [Route("api/v1/plagiarism-reports")]
  public class PlagiarismReportsController : ControllerBase
  {
    private readonly IPlagiarismReportService _reportService;

    public PlagiarismReportsController(IPlagiarismReportService reportService)
    {
      _reportService = reportService;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateReport([FromBody] CreatePlagiarismReportRequest request)
    {
      var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!int.TryParse(userIdStr, out var userId))
        return Unauthorized();

      var result = await _reportService.CreateReportAsync(userId, request);
      return Ok(result);
    }

    [HttpGet("moderator")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetPendingReports([FromQuery] int page = 1, [FromQuery] int limit = 20)
    {
      var result = await _reportService.GetPendingReportsAsync(page, limit);
      return Ok(result);
    }

    [HttpPost("moderator/{id}/resolve")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> ResolveReport(int id, [FromBody] ResolvePlagiarismReportRequest request)
    {
      var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
      if (!int.TryParse(userIdStr, out var userId))
        return Unauthorized();

      try
      {
        var result = await _reportService.ResolveReportAsync(id, userId, request);
        return Ok(result);
      }
      catch (KeyNotFoundException ex)
      {
        return NotFound(new { message = ex.Message });
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new { message = ex.Message });
      }
    }

    [HttpGet("moderator/{id}/compare")]
    [Authorize(Roles = "Admin,Moderator")]
    public async Task<IActionResult> GetCompareData(int id, [FromQuery] int referenceTranslationId)
    {
      try
      {
        var result = await _reportService.GetCompareDataAsync(id, referenceTranslationId);
        return Ok(result);
      }
      catch (KeyNotFoundException ex)
      {
        return NotFound(new { message = ex.Message });
      }
      catch (InvalidOperationException ex)
      {
        return BadRequest(new { message = ex.Message });
      }
    }
  }
}
