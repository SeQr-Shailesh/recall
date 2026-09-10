namespace SeQrRecall.Application.Dtos.Users;

public sealed class UserDto
{
    public Guid Id { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string? Mobile { get; init; }

    public string? Email { get; init; }

    public string? ProfilePhotoUrl { get; init; }

    public bool IsProfileComplete { get; init; }

    public DateTimeOffset CreatedOn { get; init; }
}
