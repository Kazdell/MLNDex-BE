using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Application.DTOs.ReportSystem;

namespace Application.Interfaces.ReportSystem
{
  public interface ITrustScoreService
  {
    /// <summary>Admin manually restores trust score for a User or Team.</summary>
    Task<TrustScoreRestoreResultDto> RestoreTrustScoreAsync(RestoreTrustScoreRequest request, int moderatorId, CancellationToken ct = default);

    /// <summary>User submits an appeal against a penalty.</summary>
    Task<AppealDto> CreateAppealAsync(int userId, CreateAppealRequest request, CancellationToken ct = default);

    /// <summary>Moderator reviews (approves/rejects) an appeal.</summary>
    Task<AppealDto> ReviewAppealAsync(int appealId, int moderatorId, ReviewAppealRequest request, CancellationToken ct = default);

    /// <summary>Get pending appeals for moderator dashboard.</summary>
    Task<List<AppealDto>> GetPendingAppealsAsync(int page = 1, int limit = 20, CancellationToken ct = default);

    /// <summary>Get translation portfolio/history for a user.</summary>
    Task<List<UserTranslationHistoryDto>> GetUserTranslationHistoryAsync(int userId, CancellationToken ct = default);
  }
}
