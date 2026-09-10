namespace SeQrRecall.Application.Customers;

/// <summary>
/// Shared photo MIME validation. The live size cap is <c>Uploads:PhotoMaxBytes</c> (default <see cref="DefaultMaxBytes"/>).
/// </summary>
public static class PhotoUploadRules
{
    public const long DefaultMaxBytes = 5 * 1024 * 1024;

    public const long MaxBytes = DefaultMaxBytes;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp"
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
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };
    }

    public static string ResolveContentType(string? declaredContentType, string? originalFileName)
    {
        if (IsAllowedContentType(declaredContentType))
        {
            string normalized = NormalizeContentType(declaredContentType);
            return string.Equals(normalized, "image/jpg", StringComparison.OrdinalIgnoreCase)
                ? "image/jpeg"
                : normalized;
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
