namespace SeQrRecall.Application.Configuration;

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public string Provider { get; set; } = "Dummy";
}
