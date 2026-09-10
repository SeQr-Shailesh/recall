namespace SeQrRecall.Application.Dtos.Customers;

public sealed class CreateCustomerRequest
{
    public required string Name { get; init; }

    public string? CompanyName { get; init; }

    public string? Mobile { get; init; }

    public string? Email { get; init; }
}

public sealed class UpdateCustomerRequest
{
    public required string Name { get; init; }

    public string? CompanyName { get; init; }

    public string? Mobile { get; init; }

    public string? Email { get; init; }
}
