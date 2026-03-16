using Application.Interfaces.AIModeration;
using System.Threading.Channels;

namespace Infrastructure.Services.AIModeration
{
    /// <summary>
    /// Bounded in-memory queue backed by System.Threading.Channels.
    /// Capacity = 500 jobs. When full, enqueue blocks (back-pressure).
    /// Registered as Singleton so all producers/consumers share the same queue.
    /// </summary>
    public class ModerationQueue : IModerationQueue
    {
        private readonly Channel<ModerationJob> _channel;

        public ModerationQueue()
        {
            var options = new BoundedChannelOptions(500)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,   // Only ModerationWorker reads
                SingleWriter = false   // Multiple controllers can enqueue
            };
            _channel = Channel.CreateBounded<ModerationJob>(options);
        }

        public async ValueTask EnqueueAsync(ModerationJob job, CancellationToken ct = default)
        {
            await _channel.Writer.WriteAsync(job, ct);
        }

        public async ValueTask<ModerationJob> DequeueAsync(CancellationToken ct)
        {
            return await _channel.Reader.ReadAsync(ct);
        }
    }
}
