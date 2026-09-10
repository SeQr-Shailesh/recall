namespace SeQrRecall.Domain.Entities;

/// <summary>
/// Lightweight audit record for important operations. Do not store secrets or audio contents.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    public string? Details { get; set; }

    public DateTimeOffset CreatedOn { get; set; }
}
