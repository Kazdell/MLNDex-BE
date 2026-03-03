using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation;
using Application.Interfaces.Translation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using mlndex_backend.Controllers;

namespace mlndex_backend.Controllers.Translation
{
    [Route("api/translation-teams")]
    public class TranslationTeamsController : BaseController
    {
        private readonly ITranslationTeamService _service;

        public TranslationTeamsController(ITranslationTeamService service)
        {
            _service = service;
        }

        // Create a new translation team.
        [HttpPost]
        public async Task<IActionResult> CreateTeam([FromBody] CreateTranslationTeamDto dto)
        {
            try
            {
                int leaderId = 1; // TODO: Get from Auth claims
                var team = await _service.CreateTeamAsync(leaderId, dto);
                return CreatedAtAction(nameof(GetTeamById), new { id = team.TeamId }, team);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // List all translation teams.
        [HttpGet]
        public async Task<IActionResult> GetAllTeams()
        {
            var teams = await _service.GetAllTeamsAsync();
            return OkResponse(teams);
        }

        // Get team details by ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetTeamById(int id)
        {
            var team = await _service.GetTeamByIdAsync(id);
            if (team == null) return NotFoundResponse();
            return OkResponse(team);
        }

        // Disband a translation team (Leader only).
        [HttpDelete("{id}")]
        public async Task<IActionResult> DisbandTeam(int id)
        {
            try
            {
                int leaderId = 1; // TODO: Get from Auth claims
                var success = await _service.DisbandTeamAsync(id, leaderId);
                if (!success) return NotFoundResponse("Team not found or you are not the leader.");
                
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // Invite a user to join the team.
        [HttpPost("{id}/members")]
        public async Task<IActionResult> InviteMember(int id, [FromBody] InviteTeamMemberDto dto)
        {
            try
            {
                int leaderId = 1; // TODO: Get from Auth claims
                var member = await _service.InviteMemberAsync(id, leaderId, dto);
                return OkResponse(member);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // Remove a member from the team.
        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(int id, int userId)
        {
            try
            {
                int leaderId = 1; // TODO: Get from Auth claims
                var success = await _service.RemoveMemberAsync(id, leaderId, userId);
                if (!success) return NotFoundResponse("Member not found.");
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }

        // Assign a role to a team member.
        [HttpPut("{id}/members/{userId}/role")]
        public async Task<IActionResult> AssignRole(int id, int userId, [FromBody] AssignTeamMemberRoleDto dto)
        {
            try
            {
                int leaderId = 1; // TODO: Get from Auth claims
                var member = await _service.AssignRoleAsync(id, leaderId, userId, dto);
                return OkResponse(member);
            }
            catch (Exception ex)
            {
                return BadRequestResponse(ex.Message);
            }
        }
    }
}
