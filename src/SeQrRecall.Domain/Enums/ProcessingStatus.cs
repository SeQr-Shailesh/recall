namespace SeQrRecall.Domain.Enums;

/// <summary>
/// Processing lifecycle for notes and customer interactions.
/// </summary>
/// <remarks>
/// When speech-to-text succeeds and summarization fails, the record is stored as
/// <see cref="Completed"/> with <c>ProcessingError</c> populated and summary fields left null.
/// The transcript is never discarded. See docs/Architecture.md.
/// </remarks>
public enum ProcessingStatus
{
    Draft = 0,
    Uploading = 1,
    Uploaded = 2,
    Processing = 3,
    Completed = 4,
    Failed = 5
}
