namespace SeQrRecall.Application.Abstractions.Storage;

public enum FileCategory
{
    NoteAudio = 0,
    InteractionAudio = 1,
    CustomerPhoto = 2,
    InteractionPhoto = 3,
    ProfilePhoto = 4,
    LeadAudio = 5,
    LeadPhoto = 6
}

public sealed class StoredFile
{
    public required string StoredFileName { get; init; }

    public required string RelativePath { get; init; }

    public required string ContentType { get; init; }

    public required long SizeBytes { get; init; }
}

/// <summary>
/// File storage abstraction. Business logic never depends on a physical disk path.
/// </summary>
public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(
        Stream content,
        string contentType,
        FileCategory category,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storedFileName,
        FileCategory category,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storedFileName,
        FileCategory category,
        CancellationToken cancellationToken = default);
}
