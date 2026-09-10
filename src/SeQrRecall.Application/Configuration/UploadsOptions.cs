namespace SeQrRecall.Application.Configuration;

public sealed class UploadsOptions
{
    public const string SectionName = "Uploads";

    /// <summary>
    /// Absolute ceiling for Kestrel, IIS, and request-size attributes. Configured caps cannot exceed this.
    /// </summary>
    public const long HardCeilingBytes = 100L * 1024 * 1024;

    public long AudioMaxBytes { get; set; } = 25L * 1024 * 1024;

    public long PhotoMaxBytes { get; set; } = 5L * 1024 * 1024;

    public long MultipartBodyLimitBytes()
    {
        long configured = Math.Max(AudioMaxBytes, PhotoMaxBytes) + 1024;
        return Math.Min(configured, HardCeilingBytes);
    }
}
