namespace SeQrRecall.Application.Dtos.Users;

public sealed class UpdateProfileRequest
{
    public required string FullName { get; init; }

    public string? Mobile { get; init; }

    public string? Email { get; init; }
}
