namespace SeQrRecall.Application.Dtos.Auth;

public sealed class SendOtpRequest
{
    public string? Mobile { get; init; }

    public string? Email { get; init; }
}
