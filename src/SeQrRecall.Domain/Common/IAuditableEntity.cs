namespace SeQrRecall.Domain.Common;

/// <summary>
/// Standard audit fields required on persisted entities.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedOn { get; set; }

    Guid? CreatedBy { get; set; }

    DateTimeOffset? UpdatedOn { get; set; }

    Guid? UpdatedBy { get; set; }
}
