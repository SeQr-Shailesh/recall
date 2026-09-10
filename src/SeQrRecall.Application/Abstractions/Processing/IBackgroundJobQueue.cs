namespace SeQrRecall.Application.Abstractions.Processing;

/// <summary>
/// Application-level job queue. Phase 1 uses an in-process implementation suitable for IIS.
/// Later implementations can wrap Hangfire, Azure Queue, SQS, or RabbitMQ without changing callers.
/// </summary>
public interface IBackgroundJobQueue
{
    ValueTask QueueNoteProcessingAsync(Guid noteId, CancellationToken cancellationToken = default);

    ValueTask QueueCustomerInteractionProcessingAsync(Guid interactionId, CancellationToken cancellationToken = default);

    ValueTask QueueLeadProcessingAsync(Guid leadId, CancellationToken cancellationToken = default);
}
