using Application.DTOs.Common;
using Application.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Application.DTOs.Translation.Requests;
using Application.DTOs.Translation.Responses;
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
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateTeam([FromBody] CreateTranslationTeamRequest dto)
    {
      var team = await _service.CreateTeamAsync(dto);
      return CreatedAtAction(nameof(GetTeamById), new { id = team.TeamId }, team);
    }

    // List all translation teams.
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TranslationTeamResponse>>> GetAllTeams()
    {
      var teams = await _service.GetAllTeamsAsync();
      return Ok(teams);
    }

    [HttpGet("{id}/members")]
    public async Task<ActionResult<IEnumerable<TeamMemberDetailResponse>>> GetMembers(int id)
    {
      var members = await _service.GetTeamMembersAsync(id);
      return Ok(members);
    }

    // Get current user's joined teams.
    [Authorize]
    [HttpGet("my-teams")]
    public async Task<IActionResult> GetMyTeams([FromQuery] int limit = 5)
    {
      var userId = GetUserId();
      if (userId == 0) throw new AppException(ErrorCodes.UNAUTHORIZED);

      var teams = await _service.GetUserTeamsAsync(userId, limit);
      return OkResponse(teams);
    }

    // Update team details (Leader only).
    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTeam(int id, [FromBody] UpdateTranslationTeamRequest dto)
    {
      var team = await _service.UpdateTeamAsync(id, dto);
      return OkResponse(team);
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
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DisbandTeam(int id)
    {
      var success = await _service.DisbandTeamAsync(id);
      if (!success) throw new AppException(ErrorCodes.TEAM_NOT_FOUND);

      return NoContent();
    }

    [Authorize]
    [HttpPost("{id}/invitations")]
    public async Task<IActionResult> InviteMember(int id, [FromBody] InviteTeamMemberRequest dto)
    {
      var invitationId = await _service.InviteMemberAsync(id, dto);
      return OkResponse(new { invitationId });
    }

    [Authorize]
    [HttpGet("{id}/invitations")]
    public async Task<IActionResult> GetTeamInvitations(int id)
    {
      var invitations = await _service.GetTeamInvitationsAsync(id);
      return OkResponse(invitations);
    }

    [Authorize]
    [HttpPost("invitations/{invitationId}/accept")]
    public async Task<IActionResult> AcceptInvitation(int invitationId)
    {
      var success = await _service.AcceptInvitationAsync(invitationId);
      if (!success) throw new AppException(ErrorCodes.INVITATION_NOT_FOUND);
      return OkResponse("Invitation accepted.");
    }

    [Authorize]
    [HttpPost("invitations/{invitationId}/reject")]
    public async Task<IActionResult> RejectInvitation(int invitationId)
    {
      var success = await _service.RejectInvitationAsync(invitationId);
      if (!success) throw new AppException(ErrorCodes.INVITATION_NOT_FOUND);
      return OkResponse("Invitation rejected.");
    }

    [Authorize]
    [HttpPost("{id}/join-requests")]
    public async Task<IActionResult> RequestToJoin(int id, [FromBody] JoinTeamRequest dto)
    {
      var requestId = await _service.RequestToJoinAsync(id, dto);
      return OkResponse(new { requestId });
    }

    [Authorize]
    [HttpGet("{id}/join-requests")]
    public async Task<IActionResult> GetTeamJoinRequests(int id)
    {
      var requests = await _service.GetTeamJoinRequestsAsync(id);
      return OkResponse(requests);
    }

    [Authorize]
    [HttpPost("join-requests/{requestId}/approve")]
    public async Task<IActionResult> ApproveJoinRequest(int requestId)
    {
      var success = await _service.ApproveJoinRequestAsync(requestId);
      if (!success) throw new AppException(ErrorCodes.NOT_FOUND);
      return OkResponse("Join request approved.");
    }

    [Authorize]
    [HttpPost("join-requests/{requestId}/reject")]
    public async Task<IActionResult> RejectJoinRequest(int requestId)
    {
      var success = await _service.RejectJoinRequestAsync(requestId);
      if (!success) throw new AppException(ErrorCodes.NOT_FOUND);
      return OkResponse("Join request rejected.");
    }

    // Remove a member from the team.
    [Authorize]
    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(int id, int userId)
    {
      var success = await _service.RemoveMemberAsync(id, userId);
      if (!success) throw new AppException(ErrorCodes.NOT_TEAM_MEMBER);
      return NoContent();
    }

    // Member voluntarily leaves the team.
    [Authorize]
    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveTeam(int id)
    {
      var success = await _service.LeaveTeamAsync(id);
      if (!success) throw new AppException(ErrorCodes.TEAM_NOT_FOUND);
      return OkResponse("Successfully left the team.");
    }

    // Assign a role to a team member.
    [Authorize]
    [HttpPut("{id}/members/{userId}/role")]
    public async Task<IActionResult> AssignRole(int id, int userId, [FromBody] AssignTeamMemberRoleRequest dto)
    {
      var member = await _service.AssignRoleAsync(id, userId, dto);
      return OkResponse(member);
    }

    // Get series translated by the team
    [HttpGet("{id}/series")]
    public async Task<IActionResult> GetTeamSeries(int id)
    {
      var series = await _service.GetTeamSeriesAsync(id);
      return OkResponse(series);
    }

    // Get team statistics
    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetTeamStats(int id)
    {
      var stats = await _service.GetTeamStatsAsync(id);
      return OkResponse(stats);
    }
  }
}
