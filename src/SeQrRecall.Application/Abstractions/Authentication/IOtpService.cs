namespace SeQrRecall.Application.Abstractions.Authentication;

public enum OtpChannel
{
    Sms = 0,
    Email = 1
}

/// <summary>
/// OTP delivery and validation. Implementations: DummyOtpService (v1), then Msg91 or another provider.
/// Controllers must never validate OTP codes directly.
/// </summary>
public interface IOtpService
{
    Task SendOtpAsync(string destination, OtpChannel channel, CancellationToken cancellationToken = default);

    Task<bool> ValidateOtpAsync(string destination, string code, CancellationToken cancellationToken = default);
}
