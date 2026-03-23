// Infrastructure/Hubs/ModerationHub.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Hubs;

/// <summary>
/// Hub để frontend lắng nghe tiến trình AI moderation theo từng chapter.
/// 
/// Groups:
///   "chapter_{chapterId}" — trang /moderation-result?chapterId=X join vào đây
///                           để nhận: ModerationProcessing, ModerationCompleted, ModerationFailed
///
/// Events worker push về client:
///   - ModerationProcessing  : { chapterId }
///   - ModerationCompleted   : { chapterId, result: AiModerationResultDto }
///   - ModerationFailed      : { chapterId, message }
/// </summary>
[Authorize]
public class ModerationHub : Hub
{
  private readonly ILogger<ModerationHub> _logger;

  public ModerationHub(ILogger<ModerationHub> logger)
  {
    _logger = logger;
  }

  // ── Khi frontend vào trang /moderation-result?chapterId=X ────────────
  // Gọi: connection.invoke("WatchChapter", chapterId)
  public async Task WatchChapter(int chapterId)
  {
    var userId = GetUserId();
    await Groups.AddToGroupAsync(Context.ConnectionId, $"chapter_{chapterId}");
    _logger.LogInformation(
        "[ModerationHub] User {UserId} đang theo dõi ChapterId={ChapterId}.",
        userId, chapterId);
  }

  // ── Khi frontend vào trang (cleanup) ─────────────────────────────────
  // Gọi: connection.invoke("UnwatchChapter", chapterId)
  public async Task UnwatchChapter(int chapterId)
  {
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chapter_{chapterId}");
  }

  // ── Translations ──────────────────────────────────────────────────────
  public async Task WatchTranslation(int translationId)
  {
    var userId = GetUserId();
    await Groups.AddToGroupAsync(Context.ConnectionId, $"translation_{translationId}");
    _logger.LogInformation(
        "[ModerationHub] User {UserId} đang theo dõi TranslationId={TranslationId}.",
        userId, translationId);
  }

  public async Task UnwatchTranslation(int translationId)
  {
    await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"translation_{translationId}");
  }

  public override async Task OnConnectedAsync()
  {
    _logger.LogInformation(
        "[ModerationHub] Connected: {ConnectionId}, User: {UserId}.",
        Context.ConnectionId, GetUserId());
    await base.OnConnectedAsync();
  }

  public override async Task OnDisconnectedAsync(Exception? exception)
  {
    _logger.LogInformation(
        "[ModerationHub] Disconnected: {ConnectionId}, User: {UserId}.",
        Context.ConnectionId, GetUserId());
    await base.OnDisconnectedAsync(exception);
  }

  // ── Helper ────────────────────────────────────────────────────────────
  private string GetUserId()
      => Context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
      ?? Context.User?.FindFirst("UserId")?.Value
      ?? "anonymous";
}
