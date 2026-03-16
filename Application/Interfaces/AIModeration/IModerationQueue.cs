namespace Application.Interfaces.AIModeration
{
    /// <summary>
    /// Type of content being moderated
    /// </summary>
    public enum ModerationContentType
    {
        Chapter,
        Series
    }

    /// <summary>
    /// In-memory queue for AI moderation jobs.
    /// Uses BoundedChannel under the hood to prevent OOM.
    /// </summary>
    public interface IModerationQueue
    {
        /// <summary>Enqueue content for AI moderation.</summary>
        ValueTask EnqueueAsync(ModerationJob job, CancellationToken ct = default);

        /// <summary>Dequeue the next job. Blocks until one is available.</summary>
        ValueTask<ModerationJob> DequeueAsync(CancellationToken ct);
    }

    /// <summary>Represents a single AI moderation job.</summary>
    public record ModerationJob(
        int ContentId,
        int UploaderId,
        ModerationContentType ContentType = ModerationContentType.Chapter,
        int RetryCount = 0
    );
}
