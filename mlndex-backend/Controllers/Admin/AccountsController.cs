using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace mlndex_backend.Controllers.Admin
{
    [Route("api/admin/accounts")]
    [Authorize(Roles = "ADMIN")]
    public class AccountsController : BaseController
    {
        private readonly IAccountModerationService _service;

        public AccountsController(IAccountModerationService service)
        {
            _service = service;
        }

        [HttpPost("{userId:int}/actions")]
        public async Task<IActionResult> ApplyAction(
            int userId,
            [FromBody] AccountActionRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            var moderatorIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            var moderatorId = moderatorIdClaim != null ? int.Parse(moderatorIdClaim.Value) : 1;

            try
            {
                var result = await _service.ApplyAsync(
                    userId,
                    moderatorId,
                    request,
                    cancellationToken
                );
                return OkResponse(result);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFoundResponse(ex.Message);
            }
        }
    }
}
