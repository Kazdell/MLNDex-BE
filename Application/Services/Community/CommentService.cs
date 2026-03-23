using Application.DTOs.Community;
using Application.Interfaces.Community;
using Application.Interfaces.Data;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services.Community
{
  public class CommentService : ICommentService
  {
    private readonly IMlndexDbContext _context;

    public CommentService(IMlndexDbContext context)
    {
      _context = context;
    }

    public async Task<CommentDto> CreateAsync(
        int userId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default
    )
    {
      if (request.ParentCommentId.HasValue)
      {
        var parentExists = await _context.Comments.AnyAsync(
            c => c.CommentId == request.ParentCommentId.Value,
            cancellationToken
        );
        if (!parentExists)
          throw new KeyNotFoundException("Parent comment không tồn tại.");
      }

      var now = DateTime.UtcNow;
      var entity = new Comment
      {
        UserId = userId,
        TargetId = request.TargetId,
        TargetType = request.TargetType,
        Content = request.Content,
        ParentCommentId = request.ParentCommentId,
        IsDeleted = false,
        CreatedAt = now,
        UpdatedAt = now,
      };

      _context.Comments.Add(entity);
      await _context.SaveChangesAsync(cancellationToken);

      var user =
          await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken)
          ?? throw new KeyNotFoundException("User không tồn tại.");

      return new CommentDto
      {
        CommentId = entity.CommentId,
        UserId = user.UserId,
        Username = user.Username,
        DisplayName = user.DisplayName,
        Content = entity.Content,
        ParentCommentId = entity.ParentCommentId,
        IsDeleted = entity.IsDeleted,
        CreatedAt = entity.CreatedAt,
        UpdatedAt = entity.UpdatedAt,
      };
    }

    public async Task<CommentListResponse> GetByTargetAsync(
        int targetId,
        CommentTargetType targetType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    )
    {
      var query = _context
          .Comments.Include(c => c.User)
          .Where(c =>
              c.TargetId == targetId
              && c.TargetType == targetType
              && c.ParentCommentId == null
          )
          .OrderByDescending(c => c.CreatedAt)
          .AsQueryable();

      var total = await query.CountAsync(cancellationToken);

      var roots = await query
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync(cancellationToken);

      var rootIds = roots.Select(r => r.CommentId).ToList();

      var replies = await _context
          .Comments.Include(c => c.User)
          .Where(c => c.ParentCommentId != null && rootIds.Contains(c.ParentCommentId.Value))
          .OrderBy(c => c.CreatedAt)
          .ToListAsync(cancellationToken);

      var rootDtos = roots
          .Select(r => new CommentDto
          {
            CommentId = r.CommentId,
            UserId = r.UserId,
            Username = r.User.Username,
            DisplayName = r.User.DisplayName,
            Content = r.IsDeleted ? "[deleted]" : r.Content,
            ParentCommentId = r.ParentCommentId,
            IsDeleted = r.IsDeleted,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            Replies = replies
                  .Where(rep => rep.ParentCommentId == r.CommentId)
                  .Select(rep => new CommentDto
                  {
                    CommentId = rep.CommentId,
                    UserId = rep.UserId,
                    Username = rep.User.Username,
                    DisplayName = rep.User.DisplayName,
                    Content = rep.IsDeleted ? "[deleted]" : rep.Content,
                    ParentCommentId = rep.ParentCommentId,
                    IsDeleted = rep.IsDeleted,
                    CreatedAt = rep.CreatedAt,
                    UpdatedAt = rep.UpdatedAt,
                  })
                  .ToList(),
          })
          .ToList();

      return new CommentListResponse
      {
        Items = rootDtos,
        TotalCount = total,
        Page = page,
        PageSize = pageSize,
      };
    }

    public async Task DeleteAsync(
        int commentId,
        int userId,
        CancellationToken cancellationToken = default
    )
    {
      var comment =
          await _context.Comments.FirstOrDefaultAsync(
              c => c.CommentId == commentId,
              cancellationToken
          ) ?? throw new KeyNotFoundException("Comment không tồn tại.");

      if (comment.UserId != userId)
        throw new UnauthorizedAccessException("Không thể xóa comment của người khác.");

      comment.IsDeleted = true;
      comment.Content = "[deleted]";
      comment.UpdatedAt = DateTime.UtcNow;
      await _context.SaveChangesAsync(cancellationToken);
    }
  }
}
