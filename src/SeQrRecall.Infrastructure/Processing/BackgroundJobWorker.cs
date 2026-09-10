using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Infrastructure.Processing;

public sealed class BackgroundJobWorker : BackgroundService
{
    private readonly InProcessBackgroundJobQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundJobWorker> _logger;

    public BackgroundJobWorker(
        InProcessBackgroundJobQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundJobWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (BackgroundJob job in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
                INoteProcessingService processor = scope.ServiceProvider.GetRequiredService<INoteProcessingService>();
                switch (job.Kind)
                {
                    case BackgroundJobKind.Note:
                        await processor.ProcessNoteAsync(job.EntityId, stoppingToken);
                        break;
                    case BackgroundJobKind.Lead:
                        await processor.ProcessLeadAsync(job.EntityId, stoppingToken);
                        break;
                    default:
                        await processor.ProcessCustomerInteractionAsync(job.EntityId, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Background job failed. Kind: {Kind}. EntityId: {EntityId}",
                    job.Kind,
                    job.EntityId);
                await TryMarkFailedAsync(job, stoppingToken);
            }
        }
    }

    private async Task TryMarkFailedAsync(BackgroundJob job, CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IApplicationDbContext db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            if (job.Kind == BackgroundJobKind.Note)
            {
                Note? note = await db.GetNoteByIdAsync(job.EntityId, cancellationToken);
                if (note is null || note.ProcessingStatus is not ProcessingStatus.Processing)
                {
                    return;
                }

                note.ProcessingStatus = ProcessingStatus.Failed;
                note.ProcessingError = "Processing failed. Please try again.";
            }
            else if (job.Kind == BackgroundJobKind.Lead)
            {
                Lead? lead = await db.GetLeadByIdAsync(job.EntityId, cancellationToken);
                if (lead is null || lead.ProcessingStatus is not ProcessingStatus.Processing)
                {
                    return;
                }

                lead.ProcessingStatus = ProcessingStatus.Failed;
                lead.ProcessingError = "Processing failed. Please try again.";
            }
            else
            {
                CustomerInteraction? interaction = await db.GetCustomerInteractionByIdAsync(
                    job.EntityId,
                    cancellationToken);
                if (interaction is null || interaction.ProcessingStatus is not ProcessingStatus.Processing)
                {
                    return;
                }

                interaction.ProcessingStatus = ProcessingStatus.Failed;
                interaction.ProcessingError = "Processing failed. Please try again.";
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Failed to mark {Kind} {EntityId} as Failed after a job error",
                job.Kind,
                job.EntityId);
        }
    }
}
