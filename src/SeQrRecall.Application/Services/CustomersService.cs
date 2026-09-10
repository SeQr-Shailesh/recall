using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Customers;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Customers;
using SeQrRecall.Application.Dtos.Customers;
using SeQrRecall.Application.Mapping;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Services;

public sealed class CustomersService : ICustomersService
{
    private const int NameMaxLength = 200;
    private const int CompanyMaxLength = 200;
    private const int MobileMaxLength = 20;
    private const int EmailMaxLength = 256;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _files;
    private readonly UploadsOptions _uploads;
    private readonly ILogger<CustomersService> _logger;

    public CustomersService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IFileStorageService files,
        IOptions<UploadsOptions> uploads,
        ILogger<CustomersService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _files = files;
        _uploads = uploads.Value;
        _logger = logger;
    }

    public async Task<PagedResult<CustomerDto>> ListAsync(
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        (IReadOnlyList<Customer> customers, int totalCount) = await _db.GetCustomersPageForUserAsync(
            userId,
            request.PageNumber,
            request.PageSize,
            request.Search,
            cancellationToken);

        IReadOnlyList<CustomerDto> items = customers.Select(static customer => customer.ToDto()).ToArray();
        return PagedResult<CustomerDto>.Create(items, request.PageNumber, request.PageSize, totalCount);
    }

    public async Task<CustomerDto> CreateAsync(
        CreateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        CustomerFields fields = NormalizeFields(request.Name, request.CompanyName, request.Mobile, request.Email);

        Customer customer = new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = fields.Name,
            CompanyName = fields.CompanyName,
            Mobile = fields.Mobile,
            Email = fields.Email,
            IsActive = true
        };
        _db.Add(customer);
        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "CustomerCreated",
            EntityType = "Customer",
            EntityId = customer.Id,
            Details = "Customer created.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} created customer {CustomerId}", userId, customer.Id);
        return customer.ToDto();
    }

    public async Task<CustomerDto> GetAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Customer customer = await GetOwnedCustomerAsync(customerId, cancellationToken);
        return customer.ToDto();
    }

    public async Task<CustomerDto> UpdateAsync(
        Guid customerId,
        UpdateCustomerRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Customer customer = await GetOwnedCustomerAsync(customerId, cancellationToken);
        CustomerFields fields = NormalizeFields(request.Name, request.CompanyName, request.Mobile, request.Email);

        customer.Name = fields.Name;
        customer.CompanyName = fields.CompanyName;
        customer.Mobile = fields.Mobile;
        customer.Email = fields.Email;

        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "CustomerUpdated",
            EntityType = "Customer",
            EntityId = customer.Id,
            Details = "Customer updated.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} updated customer {CustomerId}", userId, customer.Id);
        return customer.ToDto();
    }

    public async Task DeleteAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Customer customer = await GetOwnedCustomerAsync(customerId, cancellationToken);
        customer.IsDeleted = true;
        _db.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = "CustomerDeleted",
            EntityType = "Customer",
            EntityId = customer.Id,
            Details = "Customer soft-deleted.",
            CreatedOn = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} deleted customer {CustomerId}", userId, customer.Id);
    }

    public async Task<CustomerDto> UploadPhotoAsync(
        Guid customerId,
        Stream content,
        string? contentType,
        string? originalFileName,
        long contentLength,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Customer customer = await GetOwnedCustomerAsync(customerId, cancellationToken);

        if (contentLength <= 0)
        {
            throw new ValidationException("A photo file is required.", ["file is required."]);
        }

        if (contentLength > _uploads.PhotoMaxBytes)
        {
            throw new ValidationException("The photo is too large.");
        }

        string resolvedType = PhotoUploadRules.ResolveContentType(contentType, originalFileName);
        if (!PhotoUploadRules.IsAllowedContentType(resolvedType))
        {
            throw new ValidationException("Unsupported photo format.");
        }

        string? previousFile = customer.PhotoUrl;
        StoredFile stored;
        try
        {
            stored = await _files.SaveAsync(content, resolvedType, FileCategory.CustomerPhoto, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Photo storage failed for customer {CustomerId}", customer.Id);
            throw new ValidationException("The photo could not be stored.");
        }

        if (!string.IsNullOrWhiteSpace(previousFile))
        {
            try
            {
                await _files.DeleteAsync(previousFile, FileCategory.CustomerPhoto, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogWarning(
                    exception,
                    "Previous photo for customer {CustomerId} could not be deleted",
                    customer.Id);
            }
        }

        customer.PhotoUrl = stored.StoredFileName;
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} uploaded a photo for customer {CustomerId}", userId, customer.Id);
        return customer.ToDto();
    }

    public async Task<CustomerPhotoStream> OpenPhotoAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        Customer customer = await GetOwnedCustomerAsync(customerId, cancellationToken);
        if (string.IsNullOrWhiteSpace(customer.PhotoUrl))
        {
            throw new NotFoundException("Customer photo", customerId);
        }

        try
        {
            Stream stream = await _files.OpenReadAsync(
                customer.PhotoUrl,
                FileCategory.CustomerPhoto,
                cancellationToken);
            return new CustomerPhotoStream
            {
                Stream = stream,
                ContentType = PhotoUploadRules.ContentTypeFromStoredFileName(customer.PhotoUrl),
                FileName = customer.PhotoUrl
            };
        }
        catch (FileNotFoundException)
        {
            throw new NotFoundException("Customer photo", customerId);
        }
    }

    private async Task<Customer> GetOwnedCustomerAsync(Guid customerId, CancellationToken cancellationToken)
    {
        Guid userId = await GetAuthenticatedUserIdAsync(cancellationToken);
        Customer? customer = await _db.GetCustomerByIdForUserAsync(userId, customerId, cancellationToken);
        if (customer is null)
        {
            throw new NotFoundException("Customer", customerId);
        }

        return customer;
    }

    private async Task<Guid> GetAuthenticatedUserIdAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException();
        }

        User? user = await _db.GetUserByIdAsync(_currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException();
        }

        return user.Id;
    }

    private static CustomerFields NormalizeFields(string? name, string? companyName, string? mobile, string? email)
    {
        string trimmedName = (name ?? string.Empty).Trim();
        if (trimmedName.Length == 0)
        {
            throw new ValidationException("Customer name is required.", ["name is required."]);
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new ValidationException("Customer name is too long.", ["name is too long."]);
        }

        return new CustomerFields(
            trimmedName,
            TruncateOptional(companyName, CompanyMaxLength),
            TruncateOptional(mobile, MobileMaxLength),
            NormalizeEmail(email));
    }

    private static string? TruncateOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ValidationException("A customer field is too long.");
        }

        return trimmed;
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string value = email.Trim().ToLowerInvariant();
        if (!value.Contains('@', StringComparison.Ordinal) || value.Length > EmailMaxLength)
        {
            throw new ValidationException("The email address is not valid.");
        }

        return value;
    }

    private sealed record CustomerFields(string Name, string? CompanyName, string? Mobile, string? Email);
}
