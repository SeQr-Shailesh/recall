using System.Text.Json.Serialization;

namespace SeQrRecall.Application.Common.Models;

/// <summary>
/// Standard API envelope used by every SeQr Recall v1 endpoint.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }

    public T? Data { get; init; }

    public string? Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Errors { get; init; }

    public static ApiResponse<T> Ok(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<T> Fail(string message, IReadOnlyList<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = errors ?? Array.Empty<string>()
        };
    }
}

/// <summary>
/// Factory helpers for untyped success and error envelopes.
/// </summary>
public static class ApiResponse
{
    public static ApiResponse<object?> Ok(object? data = null, string? message = null)
    {
        return new ApiResponse<object?>
        {
            Success = true,
            Data = data,
            Message = message
        };
    }

    public static ApiResponse<object?> Fail(string message, IReadOnlyList<string>? errors = null)
    {
        return ApiResponse<object?>.Fail(message, errors);
    }
}
