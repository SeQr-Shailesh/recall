namespace SeQrRecall.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresOn { get; set; }

    public DateTimeOffset? RevokedOn { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public string? DeviceInfo { get; set; }

    public User User { get; set; } = null!;

    public bool IsActive => RevokedOn is null && DateTimeOffset.UtcNow < ExpiresOn;
}
