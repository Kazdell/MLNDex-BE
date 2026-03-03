using Application.DTOs.Moderation;
using Application.Interfaces.AIModeration;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Moderation
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModerationController : ControllerBase
    {
        private readonly IModerationService _moderationService;

        public ModerationController(IModerationService moderationService)
        {
            _moderationService = moderationService;
        }

        
    }
}
