using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Configuration;

namespace SeQrRecall.Infrastructure.Storage;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(IOptions<StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        StorageOptions storage = options.Value;
        if (string.IsNullOrWhiteSpace(storage.RootPath))
        {
            throw new InvalidOperationException("Storage:RootPath is not configured.");
        }

        _rootPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(storage.RootPath));
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFile> SaveAsync(
        Stream content,
        string contentType,
        FileCategory category,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        string extension = ExtensionFor(contentType);
        string storedFileName = $"{Guid.NewGuid():N}{extension}";
        string directory = GetCategoryDirectory(category);
        Directory.CreateDirectory(directory);

        string fullPath = Path.Combine(directory, storedFileName);
        await using FileStream fileStream = new(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fileStream, cancellationToken);

        return new StoredFile
        {
            StoredFileName = storedFileName,
            RelativePath = Path.GetRelativePath(_rootPath, fullPath).Replace('\\', '/'),
            ContentType = contentType,
            SizeBytes = fileStream.Length
        };
    }

    public Task<Stream> OpenReadAsync(
        string storedFileName,
        FileCategory category,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = ResolveExistingPath(storedFileName, category);
        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(
        string storedFileName,
        FileCategory category,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string fullPath = ResolveExistingPath(storedFileName, category);
        File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveExistingPath(string storedFileName, FileCategory category)
    {
        string safeFileName = ValidateFileName(storedFileName);
        string directory = GetCategoryDirectory(category);
        string fullPath = Path.GetFullPath(Path.Combine(directory, safeFileName));
        string directoryFullPath = Path.GetFullPath(directory);
        if (!fullPath.StartsWith(directoryFullPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested file path is not allowed.");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The requested file was not found.", safeFileName);
        }

        return fullPath;
    }

    private string GetCategoryDirectory(FileCategory category)
    {
        string relative = category switch
        {
            FileCategory.NoteAudio => Path.Combine("Audio", "Notes"),
            FileCategory.InteractionAudio => Path.Combine("Audio", "Interactions"),
            FileCategory.CustomerPhoto => Path.Combine("Photos", "Customers"),
            FileCategory.InteractionPhoto => Path.Combine("Photos", "Interactions"),
            FileCategory.ProfilePhoto => Path.Combine("Photos", "Profiles"),
            FileCategory.LeadAudio => Path.Combine("Audio", "Leads"),
            FileCategory.LeadPhoto => Path.Combine("Photos", "Leads"),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown file category.")
        };

        return Path.Combine(_rootPath, relative);
    }

    private static string ValidateFileName(string storedFileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedFileName);
        string fileName = Path.GetFileName(storedFileName);
        if (!string.Equals(fileName, storedFileName, StringComparison.Ordinal)
            || storedFileName.Contains("..", StringComparison.Ordinal)
            || storedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("The requested file name is not allowed.");
        }

        return fileName;
    }

    private static string ExtensionFor(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "audio/mp4" or "audio/m4a" or "audio/x-m4a" => ".m4a",
            "audio/mpeg" or "audio/mp3" => ".mp3",
            "audio/wav" or "audio/wave" or "audio/x-wav" => ".wav",
            "audio/webm" => ".webm",
            "audio/aac" => ".aac",
            "audio/ogg" => ".ogg",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/png" => ".png",
            "image/webp" => ".webp",
            _ => ".bin"
        };
    }
}
