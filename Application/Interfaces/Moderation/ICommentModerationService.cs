using Application.DTOs.Common;
using Application.DTOs.Community;

namespace Application.Interfaces.Moderation
{
    public interface ICommentModerationService
    {
        Task<PagedResult<CommentDto>> GetAdminCommentsAsync(
            string? search,
            string? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default
        );

        Task UpdateStatusAsync(
            int commentId,
            int moderatorId,
            string action,
            CancellationToken cancellationToken = default
        );

        Task BulkUpdateStatusAsync(
            List<int> commentIds,
            int moderatorId,
            string action,
            CancellationToken cancellationToken = default
        );
    }
}
