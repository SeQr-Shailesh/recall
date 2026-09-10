namespace SeQrRecall.Application.Common.Exceptions;

public sealed class ValidationException : Exception
{
    public ValidationException(string message)
        : this(message, Array.Empty<string>())
    {
    }

    public ValidationException(string message, IReadOnlyList<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
