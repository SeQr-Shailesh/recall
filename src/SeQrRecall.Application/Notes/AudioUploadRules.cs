namespace SeQrRecall.Application.Notes;

/// <summary>
/// Shared audio MIME validation. The live size cap is <c>Uploads:AudioMaxBytes</c> (default <see cref="DefaultMaxBytes"/>).
/// </summary>
public static class AudioUploadRules
{
    public const long DefaultMaxBytes = 25 * 1024 * 1024;

    public const long MaxBytes = DefaultMaxBytes;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/mp4",
        "audio/m4a",
        "audio/x-m4a",
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/wave",
        "audio/x-wav",
        "audio/webm",
        "audio/aac",
        "audio/ogg"
    };

    public static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        string value = contentType.Trim().ToLowerInvariant();
        int separator = value.IndexOf(';', StringComparison.Ordinal);
        if (separator >= 0)
        {
            value = value[..separator].Trim();
        }

        return value;
    }

    public static bool IsAllowedContentType(string? contentType)
    {
        string normalized = NormalizeContentType(contentType);
        return AllowedContentTypes.Contains(normalized);
    }

    public static string? InferContentTypeFromFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".m4a" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            ".aac" => "audio/aac",
            ".ogg" => "audio/ogg",
            _ => null
        };
    }

    public static string ResolveContentType(string? declaredContentType, string? originalFileName)
    {
        if (IsAllowedContentType(declaredContentType))
        {
            return NormalizeContentType(declaredContentType);
        }

        string? inferred = InferContentTypeFromFileName(originalFileName);
        if (inferred is not null)
        {
            return inferred;
        }

        return NormalizeContentType(declaredContentType);
    }

    public static string ContentTypeFromStoredFileName(string storedFileName)
    {
        return InferContentTypeFromFileName(storedFileName) ?? "application/octet-stream";
    }
}
