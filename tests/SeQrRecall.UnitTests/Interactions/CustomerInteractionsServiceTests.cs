using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Customers;
using SeQrRecall.Application.Dtos.Interactions;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Application.Notes;
using SeQrRecall.Application.Services;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;
using SeQrRecall.Infrastructure.Persistence;
using Xunit;

namespace SeQrRecall.UnitTests.Interactions;

public sealed class CustomerInteractionsServiceTests
{
    [Fact]
    public async Task Create_returns_a_draft_owned_by_the_current_user()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        CreateCustomerInteractionResponse created = await harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = harness.CustomerId
        });

        Assert.Equal(ProcessingStatus.Draft, created.ProcessingStatus);
        Assert.Equal(harness.CustomerId, created.CustomerId);
        CustomerInteraction stored = (await harness.Context.CustomerInteractions.FindAsync(created.Id))!;
        Assert.Equal(harness.UserId, stored.UserId);
    }

    [Fact]
    public async Task Create_does_not_use_another_users_customer()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        Guid otherCustomerId = Guid.NewGuid();
        harness.Context.Customers.Add(new Customer
        {
            Id = otherCustomerId,
            UserId = Guid.NewGuid(),
            Name = "Other",
            IsActive = true
        });
        await harness.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = otherCustomerId
        }));
    }

    [Fact]
    public async Task Get_does_not_return_another_users_interaction()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        Guid otherId = Guid.NewGuid();
        harness.Context.CustomerInteractions.Add(new CustomerInteraction
        {
            Id = otherId,
            UserId = Guid.NewGuid(),
            CustomerId = harness.CustomerId,
            InteractionDate = DateTimeOffset.UtcNow,
            ProcessingStatus = ProcessingStatus.Draft
        });
        await harness.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAsync(otherId));
    }

    [Fact]
    public async Task List_returns_owned_interactions_newest_first_and_omits_transcript()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        CreateCustomerInteractionResponse first = await harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = harness.CustomerId,
            InteractionDate = DateTimeOffset.UtcNow.AddDays(-1)
        });
        CreateCustomerInteractionResponse second = await harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = harness.CustomerId,
            InteractionDate = DateTimeOffset.UtcNow
        });

        PagedResult<CustomerInteractionListDto> page = await harness.Service.ListForCustomerAsync(
            harness.CustomerId,
            new PagedRequest());
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(second.Id, page.Items[0].Id);
        Assert.Equal(first.Id, page.Items[1].Id);
    }

    [Fact]
    public async Task Upload_audio_rejects_unsupported_types_and_oversize_files()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        CreateCustomerInteractionResponse created = await harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = harness.CustomerId
        });
        await using MemoryStream content = new(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "text/plain",
            "clip.txt",
            durationSeconds: null,
            contentLength: 3));

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "audio/mpeg",
            "clip.mp3",
            durationSeconds: null,
            contentLength: AudioUploadRules.MaxBytes + 1));
    }

    [Fact]
    public async Task Upload_audio_queues_processing()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        CreateCustomerInteractionResponse created = await harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = harness.CustomerId
        });
        await using MemoryStream content = new(new byte[] { 1, 2, 3, 4 });

        ProcessingStatusDto status = await harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "audio/mpeg",
            "clip.mp3",
            durationSeconds: 12,
            contentLength: 4);

        Assert.Equal(ProcessingStatus.Uploaded, status.ProcessingStatus);
        Assert.Equal(created.Id, harness.Queue.LastInteractionId);
    }

    [Fact]
    public async Task Upload_photo_stores_and_replaces_the_previous_file()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        CreateCustomerInteractionResponse created = await harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = harness.CustomerId
        });
        await using MemoryStream first = new(new byte[] { 1, 2, 3, 4 });
        CustomerInteractionDto withPhoto = await harness.Service.UploadPhotoAsync(
            created.Id,
            first,
            "image/jpeg",
            "photo.jpg",
            contentLength: 4);
        Assert.True(withPhoto.HasPhoto);

        string firstStored = (await harness.Context.CustomerInteractions.FindAsync(created.Id))!.PhotoUrl!;
        await using MemoryStream second = new(new byte[] { 9, 8, 7 });
        await harness.Service.UploadPhotoAsync(
            created.Id,
            second,
            "image/png",
            "photo.png",
            contentLength: 3);

        CustomerInteraction stored = (await harness.Context.CustomerInteractions.FindAsync(created.Id))!;
        Assert.False(string.Equals(firstStored, stored.PhotoUrl, StringComparison.Ordinal));
        Assert.Contains(firstStored, harness.Files.Deleted);

        CustomerInteractionPhotoStream photo = await harness.Service.OpenPhotoAsync(created.Id);
        await using (photo.Stream)
        {
            Assert.Equal("image/png", photo.ContentType);
        }
    }

    [Fact]
    public async Task Upload_photo_rejects_unsupported_types()
    {
        await using InteractionsServiceHarness harness = await InteractionsServiceHarness.CreateAsync();
        CreateCustomerInteractionResponse created = await harness.Service.CreateAsync(new CreateCustomerInteractionRequest
        {
            CustomerId = harness.CustomerId
        });
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

    private sealed class InteractionsServiceHarness : IAsyncDisposable
    {
        private InteractionsServiceHarness(ApplicationDbContext context, Guid userId, Guid customerId)
        {
            Context = context;
            UserId = userId;
            CustomerId = customerId;
            Queue = new RecordingQueue();
            Files = new MemoryStorage();
            Service = new CustomerInteractionsService(
                context,
                new StubCurrentUser(userId),
                Files,
                Queue,
                Options.Create(new UploadsOptions()),
                NullLogger<CustomerInteractionsService>.Instance);
        }

        public ApplicationDbContext Context { get; }

        public Guid UserId { get; }

        public Guid CustomerId { get; }

        public CustomerInteractionsService Service { get; }

        public RecordingQueue Queue { get; }

        public MemoryStorage Files { get; }

        public static async Task<InteractionsServiceHarness> CreateAsync()
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            ApplicationDbContext context = new(options);
            Guid userId = Guid.NewGuid();
            Guid customerId = Guid.NewGuid();
            context.Users.Add(new User { Id = userId, FullName = "Owner", IsActive = true });
            context.Customers.Add(new Customer
            {
                Id = customerId,
                UserId = userId,
                Name = "Rajesh Patel",
                CompanyName = "Patel Traders",
                IsActive = true
            });
            await context.SaveChangesAsync();
            return new InteractionsServiceHarness(context, userId, customerId);
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

    private sealed class RecordingQueue : IBackgroundJobQueue
    {
        public Guid? LastInteractionId { get; private set; }

        public ValueTask QueueNoteProcessingAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask QueueCustomerInteractionProcessingAsync(
            Guid interactionId,
            CancellationToken cancellationToken = default)
        {
            LastInteractionId = interactionId;
            return ValueTask.CompletedTask;
        }

        public ValueTask QueueLeadProcessingAsync(Guid leadId, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }
    }

    internal sealed class MemoryStorage : IFileStorageService
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
            string extension = contentType.Contains("png", StringComparison.OrdinalIgnoreCase)
                ? ".png"
                : contentType.Contains("image", StringComparison.OrdinalIgnoreCase)
                    ? ".jpg"
                    : ".mp3";
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
