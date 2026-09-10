using System.Threading.Channels;
using SeQrRecall.Application.Abstractions.Processing;

namespace SeQrRecall.Infrastructure.Processing;

public enum BackgroundJobKind
{
    Note = 0,
    CustomerInteraction = 1,
    Lead = 2
}

public sealed record BackgroundJob(BackgroundJobKind Kind, Guid EntityId);

/// <summary>
/// In-process channel queue suitable for a single IIS worker. Swap this type for Hangfire or a cloud queue later.
/// </summary>
public sealed class InProcessBackgroundJobQueue : IBackgroundJobQueue
{
    private readonly Channel<BackgroundJob> _jobs = Channel.CreateUnbounded<BackgroundJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask QueueNoteProcessingAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        return _jobs.Writer.WriteAsync(new BackgroundJob(BackgroundJobKind.Note, noteId), cancellationToken);
    }

    public ValueTask QueueCustomerInteractionProcessingAsync(
        Guid interactionId,
        CancellationToken cancellationToken = default)
    {
        return _jobs.Writer.WriteAsync(
            new BackgroundJob(BackgroundJobKind.CustomerInteraction, interactionId),
            cancellationToken);
    }

    public ValueTask QueueLeadProcessingAsync(Guid leadId, CancellationToken cancellationToken = default)
    {
        return _jobs.Writer.WriteAsync(new BackgroundJob(BackgroundJobKind.Lead, leadId), cancellationToken);
    }

    public IAsyncEnumerable<BackgroundJob> ReadAllAsync(CancellationToken cancellationToken)
    {
        return _jobs.Reader.ReadAllAsync(cancellationToken);
    }
}
