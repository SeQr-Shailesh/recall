namespace SeQrRecall.Application.Configuration;

public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int OtpSendPermitLimit { get; set; } = 5;

    public int OtpVerifyPermitLimit { get; set; } = 10;

    public int OtpWindowSeconds { get; set; } = 60;

    public int UploadPermitLimit { get; set; } = 20;

    public int UploadWindowSeconds { get; set; } = 60;
}
