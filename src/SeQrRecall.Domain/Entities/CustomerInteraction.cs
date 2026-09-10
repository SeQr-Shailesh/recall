using SeQrRecall.Domain.Common;
using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Domain.Entities;

public sealed class CustomerInteraction : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid CustomerId { get; set; }

    public string? PhotoUrl { get; set; }

    public string? AudioFileUrl { get; set; }

    public string? Transcript { get; set; }

    public string? ShortSummary { get; set; }

    public string? FullSummary { get; set; }

    public DateTimeOffset InteractionDate { get; set; }

    public int? DurationSeconds { get; set; }

    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Draft;

    public string? ProcessingError { get; set; }

    public string? DetectedLanguages { get; set; }

    public string? ImportantEntitiesJson { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset? CompletedOn { get; set; }

    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;

    public Customer Customer { get; set; } = null!;

    public ICollection<CustomerInteractionActionItem> ActionItems { get; set; } = new List<CustomerInteractionActionItem>();
}
