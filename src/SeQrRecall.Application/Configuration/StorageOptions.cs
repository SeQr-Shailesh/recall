namespace SeQrRecall.Application.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Provider { get; set; } = "Local";

    public string RootPath { get; set; } = string.Empty;
}
