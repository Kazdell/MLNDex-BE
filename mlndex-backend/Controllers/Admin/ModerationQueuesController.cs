using Application.DTOs.Moderation;
using Application.Interfaces.Moderation;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Admin
{
    [Route("api/admin/moderation-queues")]
    public class ModerationQueuesController : BaseController
    {
        private readonly IReportService _service;

        public ModerationQueuesController(IReportService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetQueues(CancellationToken cancellationToken)
        {
            var result = await _service.GetPendingQueuesAsync(cancellationToken);
            return OkResponse(result);
        }

        [HttpPost("{queueId:int}/decision")]
        public async Task<IActionResult> Decide(
            int queueId,
            [FromBody] ModerationDecisionRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            // TODO: replace with moderatorId from auth claims
            var moderatorId = 1;

            try
            {
                var result = await _service.DecideAsync(
                    queueId,
                    moderatorId,
                    request,
                    cancellationToken
                );
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

        [HttpPost("{queueId:int}/content-decision")]
        public async Task<IActionResult> ContentDecision(
            int queueId,
            [FromServices] IContentModerationService contentService,
            [FromBody] ContentModerationDecisionRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            // TODO: replace with moderatorId from auth claims
            var moderatorId = 1;

            try
            {
                var result = await contentService.DecideAsync(
                    queueId,
                    moderatorId,
                    request,
                    cancellationToken
                );
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

        [HttpPost("{queueId:int}/violation-feedback")]
        public async Task<IActionResult> SendFeedback(
            int queueId,
            [FromServices] IViolationFeedbackService feedbackService,
            [FromBody] ViolationFeedbackRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            // TODO: replace with moderatorId from auth claims
            var moderatorId = 1;

            try
            {
                var result = await feedbackService.SendFeedbackAsync(
                    queueId,
                    moderatorId,
                    request,
                    cancellationToken
                );
                return OkResponse(result, "Feedback sent");
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
