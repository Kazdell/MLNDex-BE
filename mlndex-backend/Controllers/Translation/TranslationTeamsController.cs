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
        var team = await _service.CreateTeamAsync(dto);
        return CreatedAtAction(nameof(GetTeamById), new { id = team.TeamId }, team);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // List all translation teams.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TranslationTeamDto>>> GetAllTeams()
    {
      var teams = await _service.GetAllTeamsAsync();
      return Ok(teams);
    }

    [HttpGet("{id}/members")]
    public async Task<ActionResult<IEnumerable<TeamMemberDetailDto>>> GetMembers(int id)
    {
      var members = await _service.GetTeamMembersAsync(id);
      return Ok(members);
    }

    // Get current user's joined teams.
    [Authorize]
    [HttpGet("my-teams")]
    public async Task<IActionResult> GetMyTeams([FromQuery] int limit = 5)
    {
      try
      {
        var userId = GetUserId();
        if (userId == 0) return UnauthorizedResponse("Invalid user.");

        var teams = await _service.GetUserTeamsAsync(userId, limit);
        return OkResponse(teams);
      }
      catch (Exception ex)
      {
        System.IO.File.WriteAllText("C:/Users/ACER/Downloads/MLNDex/error.txt", ex.ToString());
        return BadRequestResponse(ex.ToString());
      }
    }

    // Update team details (Leader only).
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTeam(int id, [FromBody] UpdateTranslationTeamDto dto)
    {
      try
      {
        var team = await _service.UpdateTeamAsync(id, dto);
        return OkResponse(team);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
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
        var success = await _service.DisbandTeamAsync(id);
        if (!success) return NotFoundResponse("Team not found or you are not the leader.");

        return NoContent();
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    [Authorize]
    [HttpPost("{id}/invitations")]
    public async Task<IActionResult> InviteMember(int id, [FromBody] InviteTeamMemberDto dto)
    {
      try
      {
        var invitationId = await _service.InviteMemberAsync(id, dto);
        return OkResponse(new { invitationId });
      }
      catch (Exception ex)
      {
        var message = ex.InnerException != null ? $"{ex.Message} | Inner: {ex.InnerException.Message}" : ex.Message;
        return BadRequestResponse(message);
      }
    }

    [Authorize]
    [HttpGet("{id}/invitations")]
    public async Task<IActionResult> GetTeamInvitations(int id)
    {
      try
      {
        var invitations = await _service.GetTeamInvitationsAsync(id);
        return OkResponse(invitations);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    [Authorize]
    [HttpPost("invitations/{invitationId}/accept")]
    public async Task<IActionResult> AcceptInvitation(int invitationId)
    {
      try
      {
        var success = await _service.AcceptInvitationAsync(invitationId);
        if (!success) return NotFoundResponse("Invitation not found or unauthorized.");
        return OkResponse("Invitation accepted.");
      }
      catch (Exception ex)
      {
        var message = ex.InnerException != null ? $"{ex.Message} | Inner: {ex.InnerException.Message}" : ex.Message;
        return BadRequestResponse(message);
      }
    }

    [Authorize]
    [HttpPost("invitations/{invitationId}/reject")]
    public async Task<IActionResult> RejectInvitation(int invitationId)
    {
      try
      {
        var success = await _service.RejectInvitationAsync(invitationId);
        if (!success) return NotFoundResponse("Invitation not found or unauthorized.");
        return OkResponse("Invitation rejected.");
      }
      catch (Exception ex)
      {
        var message = ex.InnerException != null ? $"{ex.Message} | Inner: {ex.InnerException.Message}" : ex.Message;
        return BadRequestResponse(message);
      }
    }

    [Authorize]
    [HttpPost("{id}/join-requests")]
    public async Task<IActionResult> RequestToJoin(int id, [FromBody] JoinTeamRequestDto dto)
    {
      try
      {
        var requestId = await _service.RequestToJoinAsync(id, dto);
        return OkResponse(new { requestId });
      }
      catch (Exception ex)
      {
        var message = ex.InnerException != null ? $"{ex.Message} | Inner: {ex.InnerException.Message}" : ex.Message;
        return BadRequestResponse(message);
      }
    }

    [Authorize]
    [HttpGet("{id}/join-requests")]
    public async Task<IActionResult> GetTeamJoinRequests(int id)
    {
      try
      {
        var requests = await _service.GetTeamJoinRequestsAsync(id);
        return OkResponse(requests);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    [Authorize]
    [HttpPost("join-requests/{requestId}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(int requestId)
    {
      try
      {
        var success = await _service.ApproveJoinRequestAsync(requestId);
        if (!success) return NotFoundResponse("Join request not found or unauthorized.");
        return OkResponse("Join request approved.");
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    [Authorize]
    [HttpPost("join-requests/{requestId}/reject")]
    public async Task<IActionResult> RejectJoinRequest(int requestId)
    {
      try
      {
        var success = await _service.RejectJoinRequestAsync(requestId);
        if (!success) return NotFoundResponse("Join request not found or unauthorized.");
        return OkResponse("Join request rejected.");
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
        var success = await _service.RemoveMemberAsync(id, userId);
        if (!success) return NotFoundResponse("Member not found.");
        return NoContent();
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // Member voluntarily leaves the team.
    [Authorize]
    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveTeam(int id)
    {
      try
      {
        var success = await _service.LeaveTeamAsync(id);
        if (!success) return NotFoundResponse("You are not a member of this team.");
        return OkResponse("Successfully left the team.");
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
        var member = await _service.AssignRoleAsync(id, userId, dto);
        return OkResponse(member);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // Get series translated by the team
    [HttpGet("{id}/series")]
    public async Task<IActionResult> GetTeamSeries(int id)
    {
      try
      {
        var series = await _service.GetTeamSeriesAsync(id);
        return OkResponse(series);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }

    // Get team statistics
    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetTeamStats(int id)
    {
      try
      {
        var stats = await _service.GetTeamStatsAsync(id);
        return OkResponse(stats);
      }
      catch (Exception ex)
      {
        return BadRequestResponse(ex.Message);
      }
    }
  }
}
