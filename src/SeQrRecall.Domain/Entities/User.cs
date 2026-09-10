using SeQrRecall.Domain.Common;

namespace SeQrRecall.Domain.Entities;

public sealed class User : IAuditableEntity
{
    public Guid Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Mobile { get; set; }

    public string? Email { get; set; }

    public string? ProfilePhotoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedOn { get; set; }

    public Guid? CreatedBy { get; set; }

    public DateTimeOffset? UpdatedOn { get; set; }

    public Guid? UpdatedBy { get; set; }

    public DateTimeOffset? LastLoginOn { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    public ICollection<Note> Notes { get; set; } = new List<Note>();

    public ICollection<Lead> Leads { get; set; } = new List<Lead>();

    public ICollection<Customer> Customers { get; set; } = new List<Customer>();

    public ICollection<CustomerInteraction> CustomerInteractions { get; set; } = new List<CustomerInteraction>();
}
