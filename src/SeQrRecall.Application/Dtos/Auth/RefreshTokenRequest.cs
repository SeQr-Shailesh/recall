namespace SeQrRecall.Application.Dtos.Auth;

public sealed class RefreshTokenRequest
{
    public required string RefreshToken { get; init; }

    public string? DeviceInfo { get; init; }
}
