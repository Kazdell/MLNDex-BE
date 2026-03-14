using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Admin
{
    [Route("api/admin/accounts")]
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

            // TODO: replace with moderatorId from auth claims
            var moderatorId = 1;

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
