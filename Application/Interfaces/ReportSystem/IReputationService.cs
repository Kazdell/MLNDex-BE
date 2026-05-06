using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.Common;
using Application.DTOs.ReportSystem;

namespace Application.Interfaces.ReportSystem
{
  public interface IReputationService
  {
    /// <summary>Admin manually restores reputation score for a Creator or Team.</summary>
    Task<ReputationRestoreResultDto> RestoreReputationAsync(RestoreReputationRequest request, int moderatorId, CancellationToken ct = default);

    /// <summary>Alias used by controller (same as RestoreReputationAsync).</summary>
    Task<ReputationRestoreResultDto> RestoreReputationScoreAsync(RestoreReputationRequest request, int moderatorId, CancellationToken ct = default);

    /// <summary>
    /// Core method: add or subtract points for a Creator or Team and write a ReputationHistory log.
    /// scoreChange can be negative (penalty) or positive (reward).
    /// </summary>
    Task ModifyReputationAsync(
        ReputationTargetType targetType,
        int targetId,
        int scoreChange,
        string reason,
        int? relatedReportId = null,
        CancellationToken ct = default);

    /// <summary>Get reputation history for a creator or a team (paginated).</summary>
    Task<List<ReputationHistoryDto>> GetReputationHistoryAsync(
        int? creatorId,
        int? teamId,
        int page = 1,
        int limit = 20,
        CancellationToken ct = default);

    /// <summary>User submits an appeal against a penalty.</summary>
    Task<AppealDto> CreateAppealAsync(int userId, CreateAppealRequest request, CancellationToken ct = default);

    /// <summary>Moderator reviews (approves/rejects) an appeal.</summary>
    Task<AppealDto> ReviewAppealAsync(int appealId, int moderatorId, ReviewAppealRequest request, CancellationToken ct = default);

    /// <summary>Get pending appeals for moderator dashboard.</summary>
    Task<List<AppealDto>> GetPendingAppealsAsync(int page = 1, int limit = 20, CancellationToken ct = default);

    /// <summary>Get translation portfolio/history for a user.</summary>
    Task<List<UserTranslationHistoryDto>> GetUserTranslationHistoryAsync(int userId, CancellationToken ct = default);

    /// <summary>Get reputation overview (all creators & teams) for admin dashboard.</summary>
    Task<PagedResult<ReputationOverviewDto>> GetReputationOverviewAsync(string type, string? search, int page = 1, int limit = 20, CancellationToken ct = default);
  }
}
