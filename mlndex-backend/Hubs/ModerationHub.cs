using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace mlndex_backend.Hubs
{
    /// <summary>
    /// SignalR Hub for real-time chapter moderation status updates.
    /// Clients join group "Chapter_{chapterId}" to receive AI results.
    /// </summary>
    [Authorize]
    public class ModerationHub : Hub
    {
        /// <summary>Client calls this to subscribe to a chapter's moderation result.</summary>
        public async Task JoinChapterGroup(int chapterId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Chapter_{chapterId}");
        }

        /// <summary>Client calls this when leaving the moderation result page.</summary>
        public async Task LeaveChapterGroup(int chapterId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Chapter_{chapterId}");
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // Groups are auto-cleaned by SignalR on disconnect
            await base.OnDisconnectedAsync(exception);
        }
    }
}
