namespace SeQrRecall.Application.Dtos.Auth;

public sealed class VerifyOtpRequest
{
    public string? Mobile { get; init; }

    public string? Email { get; init; }

    public required string Otp { get; init; }

    public string? DeviceInfo { get; init; }
}
