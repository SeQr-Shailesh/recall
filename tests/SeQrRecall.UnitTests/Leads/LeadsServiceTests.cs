using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Leads;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Application.Notes;
using SeQrRecall.Application.Services;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;
using SeQrRecall.Infrastructure.Persistence;
using SeQrRecall.UnitTests.Notes;
using Xunit;

namespace SeQrRecall.UnitTests.Leads;

public sealed class LeadsServiceTests
{
    [Fact]
    public async Task Create_returns_a_draft_owned_by_the_current_user()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse created = await harness.Service.CreateAsync();

        Assert.Equal(ProcessingStatus.Draft, created.ProcessingStatus);
        Lead stored = (await harness.Context.Leads.FindAsync(created.Id))!;
        Assert.Equal(harness.UserId, stored.UserId);
    }

    [Fact]
    public async Task Get_does_not_return_another_users_lead()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        Guid otherLeadId = Guid.NewGuid();
        harness.Context.Leads.Add(new Lead
        {
            Id = otherLeadId,
            UserId = Guid.NewGuid(),
            ProcessingStatus = ProcessingStatus.Draft
        });
        await harness.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAsync(otherLeadId));
    }

    [Fact]
    public async Task Upload_rejects_unsupported_types_and_oversize_files()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse created = await harness.Service.CreateAsync();
        await using MemoryStream content = new(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "text/plain",
            "lead.txt",
            durationSeconds: null,
            contentLength: 3));

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "audio/mpeg",
            "lead.mp3",
            durationSeconds: null,
            contentLength: AudioUploadRules.MaxBytes + 1));
    }

    [Fact]
    public async Task Upload_queues_processing_and_sets_uploaded()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse created = await harness.Service.CreateAsync();
        await using MemoryStream content = new(new byte[] { 1, 2, 3, 4 });

        ProcessingStatusDto status = await harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "audio/mpeg",
            "lead.mp3",
            durationSeconds: 12,
            contentLength: 4);

        Assert.Equal(ProcessingStatus.Uploaded, status.ProcessingStatus);
        Assert.Equal(created.Id, harness.Queue.LastLeadId);
        Lead stored = (await harness.Context.Leads.FindAsync(created.Id))!;
        Assert.False(string.IsNullOrWhiteSpace(stored.AudioFileUrl));
        Assert.Equal(12, stored.DurationSeconds);
    }

    [Fact]
    public async Task Photo_can_be_attached_before_any_audio_is_uploaded()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse created = await harness.Service.CreateAsync();
        await using MemoryStream photo = new(new byte[] { 1, 2, 3, 4 });

        LeadDetailsDto details = await harness.Service.UploadPhotoAsync(
            created.Id,
            photo,
            "image/jpeg",
            "lead.jpg",
            contentLength: 4);

        Assert.True(details.HasPhoto);
        Assert.False(details.HasAudio);
        Assert.Equal(ProcessingStatus.Draft, details.ProcessingStatus);
    }

    [Fact]
    public async Task Photo_upload_replaces_the_previous_file()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse created = await harness.Service.CreateAsync();

        await using MemoryStream first = new(new byte[] { 1, 2, 3, 4 });
        await harness.Service.UploadPhotoAsync(created.Id, first, "image/jpeg", "one.jpg", contentLength: 4);
        string firstFile = (await harness.Context.Leads.FindAsync(created.Id))!.PhotoUrl!;

        await using MemoryStream second = new(new byte[] { 5, 6, 7, 8 });
        await harness.Service.UploadPhotoAsync(created.Id, second, "image/png", "two.png", contentLength: 4);
        string secondFile = (await harness.Context.Leads.FindAsync(created.Id))!.PhotoUrl!;

        Assert.NotEqual(firstFile, secondFile);
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => harness.Files.OpenReadAsync(firstFile, FileCategory.LeadPhoto));
    }

    [Fact]
    public async Task Photo_upload_rejects_unsupported_types_and_oversize_files()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync(new UploadsOptions
        {
            PhotoMaxBytes = 4
        });
        CreateLeadResponse created = await harness.Service.CreateAsync();
        await using MemoryStream content = new(new byte[] { 1, 2, 3, 4, 5 });

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadPhotoAsync(
            created.Id,
            content,
            "application/pdf",
            "lead.pdf",
            contentLength: 4));

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadPhotoAsync(
            created.Id,
            content,
            "image/jpeg",
            "lead.jpg",
            contentLength: 5));
    }

    [Fact]
    public async Task Photo_download_is_not_found_when_no_photo_was_attached()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse created = await harness.Service.CreateAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.OpenPhotoAsync(created.Id));
    }

    [Fact]
    public async Task List_reports_has_photo_so_clients_can_render_a_thumbnail()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse withPhoto = await harness.Service.CreateAsync();
        CreateLeadResponse withoutPhoto = await harness.Service.CreateAsync();

        await using MemoryStream photo = new(new byte[] { 1, 2, 3, 4 });
        await harness.Service.UploadPhotoAsync(withPhoto.Id, photo, "image/jpeg", "lead.jpg", contentLength: 4);

        PagedResult<LeadListDto> page = await harness.Service.ListAsync(new PagedRequest());

        Assert.True(page.Items.Single(item => item.Id == withPhoto.Id).HasPhoto);
        Assert.False(page.Items.Single(item => item.Id == withoutPhoto.Id).HasPhoto);
    }

    [Fact]
    public async Task List_omits_deleted_leads()
    {
        await using LeadsServiceHarness harness = await LeadsServiceHarness.CreateAsync();
        CreateLeadResponse created = await harness.Service.CreateAsync();
        await harness.Service.DeleteAsync(created.Id);

        PagedResult<LeadListDto> page = await harness.Service.ListAsync(new PagedRequest());
        Assert.Empty(page.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAsync(created.Id));
    }

    private sealed class LeadsServiceHarness : IAsyncDisposable
    {
        private LeadsServiceHarness(ApplicationDbContext context, Guid userId, UploadsOptions uploads)
        {
            Context = context;
            UserId = userId;
            Queue = new RecordingQueue();
            Files = new NoteProcessingServiceTests.MemoryFileStorage();
            Service = new LeadsService(
                context,
                new StubCurrentUser(userId),
                Files,
                Queue,
                Options.Create(uploads),
                NullLogger<LeadsService>.Instance);
        }

        public ApplicationDbContext Context { get; }

        public Guid UserId { get; }

        public LeadsService Service { get; }

        public RecordingQueue Queue { get; }

        public IFileStorageService Files { get; }

        public static async Task<LeadsServiceHarness> CreateAsync(UploadsOptions? uploads = null)
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            ApplicationDbContext context = new(options);
            Guid userId = Guid.NewGuid();
            context.Users.Add(new User { Id = userId, FullName = "Owner", IsActive = true });
            await context.SaveChangesAsync();
            return new LeadsServiceHarness(context, userId, uploads ?? new UploadsOptions());
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
        public Guid? LastLeadId { get; private set; }

        public ValueTask QueueNoteProcessingAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask QueueCustomerInteractionProcessingAsync(
            Guid interactionId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.CompletedTask;
        }

        public ValueTask QueueLeadProcessingAsync(Guid leadId, CancellationToken cancellationToken = default)
        {
            LastLeadId = leadId;
            return ValueTask.CompletedTask;
        }
    }
}
