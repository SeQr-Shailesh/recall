namespace SeQrRecall.Infrastructure.Speech;

internal sealed class SarvamTranscriptionResponse
{
    public string? RequestId { get; set; }

    public string? Transcript { get; set; }

    public string? LanguageCode { get; set; }
}

internal sealed class SarvamErrorResponse
{
    public SarvamErrorDetails? Error { get; set; }
}

internal sealed class SarvamErrorDetails
{
    public string? Message { get; set; }

    public string? Code { get; set; }

    public string? RequestId { get; set; }
}

internal sealed class SarvamJobInitRequest
{
    public required SarvamJobParameters JobParameters { get; init; }
}

internal sealed class SarvamJobParameters
{
    public required string Model { get; init; }

    public required string Mode { get; init; }

    public required string LanguageCode { get; init; }
}

internal sealed class SarvamJobInitResponse
{
    public string? JobId { get; set; }

    public string? JobState { get; set; }

    public string? StorageContainerType { get; set; }
}

internal sealed class SarvamFilesRequest
{
    public required string JobId { get; init; }

    public required IReadOnlyList<string> Files { get; init; }
}

internal sealed class SarvamFilesUploadResponse
{
    public string? JobId { get; set; }

    public string? JobState { get; set; }

    public string? StorageContainerType { get; set; }

    public Dictionary<string, SarvamSignedUrl>? UploadUrls { get; set; }
}

internal sealed class SarvamFilesDownloadResponse
{
    public string? JobId { get; set; }

    public string? JobState { get; set; }

    public Dictionary<string, SarvamSignedUrl>? DownloadUrls { get; set; }
}

internal sealed class SarvamSignedUrl
{
    public string? FileUrl { get; set; }
}

internal sealed class SarvamJobStatusResponse
{
    public string? JobId { get; set; }

    public string? JobState { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<SarvamJobFileDetail>? JobDetails { get; set; }
}

internal sealed class SarvamJobFileDetail
{
    public string? State { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<SarvamJobFileName>? Outputs { get; set; }
}

internal sealed class SarvamJobFileName
{
    public string? FileName { get; set; }
}
