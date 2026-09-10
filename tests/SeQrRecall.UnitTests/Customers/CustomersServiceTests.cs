using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Customers;
using SeQrRecall.Application.Dtos.Customers;
using SeQrRecall.Application.Services;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Infrastructure.Persistence;
using Xunit;

namespace SeQrRecall.UnitTests.Customers;

public sealed class CustomersServiceTests
{
    [Fact]
    public async Task Create_returns_an_owned_customer()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        CustomerDto created = await harness.Service.CreateAsync(new CreateCustomerRequest
        {
            Name = "  Rajesh Patel  ",
            CompanyName = " Patel Traders ",
            Mobile = " 9876543210 ",
            Email = "  Rajesh@Example.Test "
        });

        Assert.Equal("Rajesh Patel", created.Name);
        Assert.Equal("Patel Traders", created.CompanyName);
        Assert.Equal("9876543210", created.Mobile);
        Assert.Equal("rajesh@example.test", created.Email);
        Assert.False(created.HasPhoto);
        Assert.True(created.IsActive);

        Customer stored = (await harness.Context.Customers.FindAsync(created.Id))!;
        Assert.Equal(harness.UserId, stored.UserId);
        Assert.Null(stored.PhotoUrl);
    }

    [Fact]
    public async Task Create_rejects_a_blank_name()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.CreateAsync(new CreateCustomerRequest
        {
            Name = "   "
        }));
    }

    [Fact]
    public async Task Create_rejects_an_invalid_email()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.CreateAsync(new CreateCustomerRequest
        {
            Name = "Rajesh",
            Email = "not-an-email"
        }));
    }

    [Fact]
    public async Task Get_does_not_return_another_users_customer()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        Guid otherCustomerId = Guid.NewGuid();
        harness.Context.Customers.Add(new Customer
        {
            Id = otherCustomerId,
            UserId = Guid.NewGuid(),
            Name = "Other",
            IsActive = true
        });
        await harness.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAsync(otherCustomerId));
    }

    [Fact]
    public async Task List_searches_owned_customers_and_omits_deleted()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        CustomerDto kept = await harness.Service.CreateAsync(new CreateCustomerRequest
        {
            Name = "Anita Shah",
            CompanyName = "Shah Exports"
        });
        CustomerDto deleted = await harness.Service.CreateAsync(new CreateCustomerRequest
        {
            Name = "Anita Other"
        });
        await harness.Service.DeleteAsync(deleted.Id);

        PagedResult<CustomerDto> page = await harness.Service.ListAsync(new PagedRequest { Search = "Shah" });
        Assert.Contains(page.Items, item => item.Id == kept.Id);
        Assert.DoesNotContain(page.Items, item => item.Id == deleted.Id);
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAsync(deleted.Id));
    }

    [Fact]
    public async Task Update_changes_fields_for_the_owner()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        CustomerDto created = await harness.Service.CreateAsync(new CreateCustomerRequest { Name = "Old Name" });

        CustomerDto updated = await harness.Service.UpdateAsync(created.Id, new UpdateCustomerRequest
        {
            Name = "New Name",
            CompanyName = "New Co",
            Mobile = "111",
            Email = "new@example.test"
        });

        Assert.Equal("New Name", updated.Name);
        Assert.Equal("New Co", updated.CompanyName);
        Assert.Equal("111", updated.Mobile);
        Assert.Equal("new@example.test", updated.Email);
    }

    [Fact]
    public async Task Open_photo_without_a_file_is_not_found()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        CustomerDto created = await harness.Service.CreateAsync(new CreateCustomerRequest { Name = "Rajesh" });
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.OpenPhotoAsync(created.Id));
    }

    [Fact]
    public async Task Upload_rejects_unsupported_types_and_oversize_files()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        CustomerDto created = await harness.Service.CreateAsync(new CreateCustomerRequest { Name = "Rajesh" });
        await using MemoryStream content = new(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadPhotoAsync(
            created.Id,
            content,
            "text/plain",
            "photo.txt",
            contentLength: 3));

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadPhotoAsync(
            created.Id,
            content,
            "image/jpeg",
            "photo.jpg",
            contentLength: PhotoUploadRules.MaxBytes + 1));
    }

    [Fact]
    public async Task Upload_stores_photo_and_replaces_the_previous_file()
    {
        await using CustomersServiceHarness harness = await CustomersServiceHarness.CreateAsync();
        CustomerDto created = await harness.Service.CreateAsync(new CreateCustomerRequest { Name = "Rajesh" });
        await using MemoryStream first = new(new byte[] { 1, 2, 3, 4 });
        CustomerDto withPhoto = await harness.Service.UploadPhotoAsync(
            created.Id,
            first,
            "image/jpeg",
            "photo.jpg",
            contentLength: 4);
        Assert.True(withPhoto.HasPhoto);

        string firstStored = (await harness.Context.Customers.FindAsync(created.Id))!.PhotoUrl!;
        await using MemoryStream second = new(new byte[] { 9, 8, 7 });
        await harness.Service.UploadPhotoAsync(
            created.Id,
            second,
            "image/png",
            "photo.png",
            contentLength: 3);

        Customer stored = (await harness.Context.Customers.FindAsync(created.Id))!;
        Assert.False(string.Equals(firstStored, stored.PhotoUrl, StringComparison.Ordinal));
        Assert.Contains(firstStored, harness.Files.Deleted);

        CustomerPhotoStream photo = await harness.Service.OpenPhotoAsync(created.Id);
        await using (photo.Stream)
        {
            Assert.Equal("image/png", photo.ContentType);
        }
    }

    private sealed class CustomersServiceHarness : IAsyncDisposable
    {
        private CustomersServiceHarness(ApplicationDbContext context, Guid userId)
        {
            Context = context;
            UserId = userId;
            Files = new MemoryPhotoStorage();
            Service = new CustomersService(
                context,
                new StubCurrentUser(userId),
                Files,
                Options.Create(new UploadsOptions()),
                NullLogger<CustomersService>.Instance);
        }

        public ApplicationDbContext Context { get; }

        public Guid UserId { get; }

        public CustomersService Service { get; }

        public MemoryPhotoStorage Files { get; }

        public static async Task<CustomersServiceHarness> CreateAsync()
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            ApplicationDbContext context = new(options);
            Guid userId = Guid.NewGuid();
            context.Users.Add(new User { Id = userId, FullName = "Owner", IsActive = true });
            await context.SaveChangesAsync();
            return new CustomersServiceHarness(context, userId);
        }

        public ValueTask DisposeAsync()
        {
            return Context.DisposeAsync();
        }
    }

    private sealed class StubCurrentUser : ICurrentUserService
    {
        public StubCurrentUser(Guid userId)
        {
            UserId = userId;
        }

        public Guid UserId { get; }

        public bool IsAuthenticated => true;
    }

    internal sealed class MemoryPhotoStorage : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> Deleted { get; } = new(StringComparer.OrdinalIgnoreCase);

        public async Task<StoredFile> SaveAsync(
            Stream content,
            string contentType,
            FileCategory category,
            CancellationToken cancellationToken = default)
        {
            await using MemoryStream copy = new();
            await content.CopyToAsync(copy, cancellationToken);
            string extension = contentType.Contains("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
            string name = $"{Guid.NewGuid():N}{extension}";
            _files[name] = copy.ToArray();
            return new StoredFile
            {
                StoredFileName = name,
                RelativePath = name,
                ContentType = contentType,
                SizeBytes = copy.Length
            };
        }

        public Task<Stream> OpenReadAsync(
            string storedFileName,
            FileCategory category,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_files.TryGetValue(storedFileName, out byte[]? bytes))
            {
                throw new FileNotFoundException("The requested file was not found.", storedFileName);
            }

            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }

        public Task DeleteAsync(
            string storedFileName,
            FileCategory category,
            CancellationToken cancellationToken = default)
        {
            _files.Remove(storedFileName);
            Deleted.Add(storedFileName);
            return Task.CompletedTask;
        }
    }
}
