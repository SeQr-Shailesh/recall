using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Customers;

namespace SeQrRecall.Application.Abstractions.Customers;

public interface ICustomersService
{
    Task<PagedResult<CustomerDto>> ListAsync(PagedRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerDto> GetAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<CustomerDto> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<CustomerDto> UploadPhotoAsync(
        Guid customerId,
        Stream content,
        string? contentType,
        string? originalFileName,
        long contentLength,
        CancellationToken cancellationToken = default);

    Task<CustomerPhotoStream> OpenPhotoAsync(Guid customerId, CancellationToken cancellationToken = default);
}
