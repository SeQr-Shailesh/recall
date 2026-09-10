namespace SeQrRecall.Application.Abstractions.Processing;

/// <summary>
/// Orchestrates STT, English normalization, summarization, and persistence for notes, leads, and interactions.
/// </summary>
public interface INoteProcessingService
{
    Task ProcessNoteAsync(Guid noteId, CancellationToken cancellationToken = default);

    Task ProcessCustomerInteractionAsync(Guid interactionId, CancellationToken cancellationToken = default);

    Task ProcessLeadAsync(Guid leadId, CancellationToken cancellationToken = default);
}
