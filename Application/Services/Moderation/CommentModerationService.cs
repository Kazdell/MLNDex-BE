using Application.DTOs.Common;
using Application.DTOs.Community;
using Application.Interfaces.Data;
using Application.Interfaces.Moderation;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Moderation
{
  public class CommentModerationService : ICommentModerationService
  {
    private readonly IMlndexDbContext _context;

    public CommentModerationService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<PagedResult<CommentDto>> GetAdminCommentsAsync(
        string? search,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
      var query = _context.Comments.Include(c => c.User).AsQueryable();

      if (!string.IsNullOrWhiteSpace(search))
      {
        query = query.Where(c =>
            c.Content.Contains(search) ||
            c.User.Username.Contains(search)
        );
      }

      if (!string.IsNullOrWhiteSpace(status))
      {
        status = status.ToLower();
        if (status == "deleted")
          query = query.Where(c => c.IsDeleted);
        else if (status == "hidden")
          query = query.Where(c => c.IsHidden && !c.IsDeleted);
        else if (status == "active")
          query = query.Where(c => !c.IsDeleted && !c.IsHidden);
      }

      var total = await query.CountAsync(cancellationToken);

      var items = await query
          .OrderByDescending(c => c.CreatedAt)
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .Select(c => new CommentDto
          {
            CommentId = c.CommentId,
            UserId = c.UserId,
            Username = c.User.Username,
            DisplayName = c.User.DisplayName,
            Content = c.Content, // Moderator needs to see the real content even if deleted/hidden!
            ParentCommentId = c.ParentCommentId,
            IsDeleted = c.IsDeleted,
            IsHidden = c.IsHidden,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
          })
          .ToListAsync(cancellationToken);

      return new PagedResult<CommentDto>
      {
        Items = items,
        TotalCount = total,
        Page = page,
        PageSize = pageSize
      };
    }

    public async Task UpdateStatusAsync(
        int commentId,
        int moderatorId,
        string action,
        CancellationToken cancellationToken = default)
    {
      var comment = await _context.Comments.FirstOrDefaultAsync(c => c.CommentId == commentId, cancellationToken)
          ?? throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.OPERATION_NOT_ALLOWED);

      ApplyAction(comment, action);
      await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BulkUpdateStatusAsync(
        List<int> commentIds,
        int moderatorId,
        string action,
        CancellationToken cancellationToken = default)
    {
      var comments = await _context.Comments
          .Where(c => commentIds.Contains(c.CommentId))
          .ToListAsync(cancellationToken);

      if (!comments.Any()) return;

      foreach (var comment in comments)
      {
        ApplyAction(comment, action);
      }

      await _context.SaveChangesAsync(cancellationToken);
    }

    private void ApplyAction(Comment comment, string action)
    {
      switch (action.ToLower())
      {
        case "delete":
          comment.IsDeleted = true;
          // Usually we don't wipe content in moderation view so we can keep evidence, 
          // but depending on business logic we might. Let's keep it for proof.
          break;
        case "hide":
          comment.IsHidden = true;
          break;
        case "restore":
          comment.IsDeleted = false;
          comment.IsHidden = false;
          break;
        default:
          throw new Application.Exceptions.AppException(Application.DTOs.Common.ErrorCodes.INVALID_MODERATOR_ACTION);
      }
      comment.UpdatedAt = DateTime.UtcNow;
    }
  }
}
