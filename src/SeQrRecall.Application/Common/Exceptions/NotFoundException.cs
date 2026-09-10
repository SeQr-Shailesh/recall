namespace SeQrRecall.Application.Common.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"{name} '{key}' was not found.")
    {
        EntityName = name;
        Key = key;
    }

    public string EntityName { get; }

    public object Key { get; }
}
