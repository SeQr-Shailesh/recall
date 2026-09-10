namespace SeQrRecall.Application.Dtos.Customers;

public sealed class CustomerPhotoStream
{
    public required Stream Stream { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}
