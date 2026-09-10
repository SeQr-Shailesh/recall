namespace SeQrRecall.Application.Dtos.Auth;

public sealed class LogoutRequest
{
    public required string RefreshToken { get; init; }
}
