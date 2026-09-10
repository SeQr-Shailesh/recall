namespace SeQrRecall.Application.Dtos.Customers;

public sealed class CustomerDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? CompanyName { get; init; }

    public string? Mobile { get; init; }

    public string? Email { get; init; }

    public bool HasPhoto { get; init; }

    public DateTimeOffset CreatedOn { get; init; }

    public bool IsActive { get; init; }
}
