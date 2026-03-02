using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace mlndex_backend.Controllers.Translation
{
    [Route("api/translation-teams")]
    [ApiController]
    public class TranslationTeamsController : ControllerBase
    {
        private readonly ITranslationTeamService _service;

        public TranslationTeamsController(ITranslationTeamService service)
        {
            _service = service;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTeam([FromBody] CreateTranslationTeamDto dto)
        {
            try
            {
                // In a real app, LeaderId comes from User/Auth claims
                // int leaderId = int.Parse(User.FindFirst("UserId").Value);
                int leaderId = 1; // MOCK ONLY
                
                var team = await _service.CreateTeamAsync(leaderId, dto);
                return CreatedAtAction(nameof(GetTeamById), new { id = team.TeamId }, team);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTeams()
        {
            var teams = await _service.GetAllTeamsAsync();
            return Ok(teams);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTeamById(int id)
        {
            var team = await _service.GetTeamByIdAsync(id);
            if (team == null) return NotFound();
            return Ok(team);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DisbandTeam(int id)
        {
            try
            {
                int leaderId = 1; // MOCK ONLY
                var success = await _service.DisbandTeamAsync(id, leaderId);
                if (!success) return NotFound(new { message = "Team not found or you are not the leader." });
                
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> InviteMember(int id, [FromBody] InviteTeamMemberDto dto)
        {
            try
            {
                int leaderId = 1; // MOCK ONLY
                var member = await _service.InviteMemberAsync(id, leaderId, dto);
                return Ok(member);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(int id, int userId)
        {
            try
            {
                int leaderId = 1; // MOCK ONLY
                var success = await _service.RemoveMemberAsync(id, leaderId, userId);
                if (!success) return NotFound(new { message = "Member not found." });
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id}/members/{userId}/role")]
        public async Task<IActionResult> AssignRole(int id, int userId, [FromBody] AssignTeamMemberRoleDto dto)
        {
            try
            {
                int leaderId = 1; // MOCK ONLY
                var member = await _service.AssignRoleAsync(id, leaderId, userId, dto);
                return Ok(member);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
