using SeQrRecall.Domain.Common;

namespace SeQrRecall.Domain.Entities;

public sealed class Customer : IAuditableEntity, ISoftDeletable
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? CompanyName { get; set; }

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public string? PhotoUrl { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public Guid? UpdatedBy { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public User User { get; set; } = null!;

    public ICollection<CustomerInteraction> Interactions { get; set; } = new List<CustomerInteraction>();
}
