using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Infrastructure.Processing;
using Xunit;

namespace SeQrRecall.UnitTests.Processing;

public sealed class InProcessBackgroundJobQueueTests
{
    [Fact]
    public async Task Queued_note_jobs_are_readable()
    {
        InProcessBackgroundJobQueue queue = new();
        Guid noteId = Guid.NewGuid();
        await queue.QueueNoteProcessingAsync(noteId);

        BackgroundJob job = await ReadOneAsync(queue);
        Assert.Equal(BackgroundJobKind.Note, job.Kind);
        Assert.Equal(noteId, job.EntityId);
    }

    [Fact]
    public async Task Queued_interaction_jobs_are_readable()
    {
        InProcessBackgroundJobQueue queue = new();
        Guid interactionId = Guid.NewGuid();
        await queue.QueueCustomerInteractionProcessingAsync(interactionId);

        BackgroundJob job = await ReadOneAsync(queue);
        Assert.Equal(BackgroundJobKind.CustomerInteraction, job.Kind);
        Assert.Equal(interactionId, job.EntityId);
    }

    private static async Task<BackgroundJob> ReadOneAsync(InProcessBackgroundJobQueue queue)
    {
        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        await foreach (BackgroundJob job in queue.ReadAllAsync(timeout.Token))
        {
            return job;
        }

        throw new InvalidOperationException("The queue produced no jobs.");
    }
}
