namespace SeQrRecall.Application.Common.Exceptions;

/// <summary>
/// Thrown when an external STT, AI, storage, or messaging provider fails.
/// Never serialize the inner exception to the client.
/// </summary>
public sealed class ExternalProviderException : Exception
{
    public ExternalProviderException(string providerName, string message)
        : base(message)
    {
        ProviderName = providerName;
    }

    public ExternalProviderException(string providerName, string message, Exception innerException)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }
}
