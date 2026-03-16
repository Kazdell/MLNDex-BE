using Application.Interfaces.Queue;
using System.Threading.Channels;

namespace Infrastructure.BackgroundJobs.Queue;

/// <summary>
/// In-memory signal queue dùng Channel[T].
/// Chỉ đóng vai trò "wake-up" cho Worker —
/// source of truth thực sự là bảng ModerationQueue trong DB.
/// </summary>
public class ModerationQueue : IModerationQueue
{
    // Bounded: tối đa 1000 signal chờ trong bộ nhớ
    // FullMode.Wait: nếu đầy thì EnqueueAsync sẽ chờ thay vì drop
    private readonly Channel<int> _channel = Channel.CreateBounded<int>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,   // chỉ 1 Worker đọc
            SingleWriter = false,  // nhiều request có thể ghi đồng thời
        });

    public ChannelWriter<int> Writer => _channel.Writer;
    public ChannelReader<int> Reader => _channel.Reader;

    /// <inheritdoc/>
    public async ValueTask EnqueueAsync(int chapterId, CancellationToken ct = default)
        => await _channel.Writer.WriteAsync(chapterId, ct);
}