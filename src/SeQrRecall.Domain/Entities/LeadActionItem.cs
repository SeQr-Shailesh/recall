using SeQrRecall.Domain.Enums;

namespace SeQrRecall.Domain.Entities;

public sealed class LeadActionItem
{
    public Guid Id { get; set; }

    public Guid LeadId { get; set; }

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset? DueDate { get; set; }

    public bool IsCompleted { get; set; }

    public ActionItemKind Kind { get; set; } = ActionItemKind.Action;

    public DateTimeOffset CreatedOn { get; set; }

    public Lead Lead { get; set; } = null!;
}
