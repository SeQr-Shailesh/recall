namespace SeQrRecall.Application.Common.Exceptions;

public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : this("You are not allowed to access this resource.")
    {
    }

    public ForbiddenAccessException(string message)
        : base(message)
    {
    }
}
