using Application.DTOs.Financial;
using Application.Interfaces.Financial;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Admin
{
  [Route("api/admin/withdrawals")]
  public class WithdrawalsController : BaseController
  {
    private readonly IWithdrawalService _service;

    public WithdrawalsController(IWithdrawalService service)
    {
      _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] WithdrawalReviewListRequest request, CancellationToken cancellationToken)
    {
      var result = await _service.GetPendingAsync(request, cancellationToken);
      return OkResponse(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
      var item = await _service.GetByIdAsync(id, cancellationToken);
      if (item == null)
        return NotFoundResponse("Withdrawal not found");

      return OkResponse(item);
    }

    [HttpPost("{id:int}/decide")]
    public async Task<IActionResult> Decide(int id, [FromBody] WithdrawalDecisionRequest request, CancellationToken cancellationToken)
    {
      if (!ModelState.IsValid)
        return BadRequestResponse("Invalid payload");

      try
      {
        var result = await _service.DecideAsync(id, request, cancellationToken);
        return OkResponse(result, "Updated");
      }
      catch (KeyNotFoundException ex)
      {
        return NotFoundResponse(ex.Message);
      }
      catch (InvalidOperationException ex)
      {
        return ConflictResponse(ex.Message);
      }
    }
  }
}
