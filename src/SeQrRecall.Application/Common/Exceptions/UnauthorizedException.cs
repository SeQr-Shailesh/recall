namespace SeQrRecall.Application.Common.Exceptions;

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException()
        : this("Authentication is required.")
    {
    }

    public UnauthorizedException(string message)
        : base(message)
    {
    }
}
