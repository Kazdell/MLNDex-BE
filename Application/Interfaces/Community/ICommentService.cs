using Application.DTOs.Community;
using Domain.Entities;

namespace Application.Interfaces.Community
{
  public interface ICommentService
  {
    Task<CommentDto> CreateAsync(
        int userId,
        CreateCommentRequest request,
        CancellationToken cancellationToken = default
    );
    Task<CommentListResponse> GetByTargetAsync(
        int targetId,
        CommentTargetType targetType,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default
    );
    Task DeleteAsync(int commentId, int userId, CancellationToken cancellationToken = default);
  }
}
