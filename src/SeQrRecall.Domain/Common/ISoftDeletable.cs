namespace SeQrRecall.Domain.Common;

/// <summary>
/// Marks an entity that is hidden from normal application operations rather than physically deleted.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
