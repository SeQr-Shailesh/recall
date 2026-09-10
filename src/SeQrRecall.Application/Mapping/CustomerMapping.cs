using SeQrRecall.Application.Dtos.Customers;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Mapping;

internal static class CustomerMapping
{
    public static CustomerDto ToDto(this Customer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return new CustomerDto
        {
            Id = customer.Id,
            Name = customer.Name,
            CompanyName = customer.CompanyName,
            Mobile = customer.Mobile,
            Email = customer.Email,
            HasPhoto = !string.IsNullOrWhiteSpace(customer.PhotoUrl),
            CreatedOn = customer.CreatedOn,
            IsActive = customer.IsActive
        };
    }
}
