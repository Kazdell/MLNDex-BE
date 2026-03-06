using Application.DTOs.System;
using Application.Interfaces.System;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Admin
{
    [Route("api/admin/system-config")]
    public class SystemConfigController : BaseController
    {
        private readonly ISystemConfigService _service;

        public SystemConfigController(ISystemConfigService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
        {
            var config = await _service.GetAsync(cancellationToken);
            return OkResponse(config);
        }

        [HttpPut]
        public async Task<IActionResult> Update(
            [FromBody] SystemConfigDto dto,
            CancellationToken cancellationToken
        )
        {
            if (!ModelState.IsValid)
                return BadRequestResponse("Invalid payload");

            try
            {
                var updated = await _service.UpdateAsync(dto, cancellationToken);
                return OkResponse(updated, "Updated");
            }
            catch (ArgumentException ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }
    }
}
