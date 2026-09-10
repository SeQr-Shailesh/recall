using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Application.Notes;
using SeQrRecall.Application.Services;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;
using SeQrRecall.Infrastructure.Persistence;
using Xunit;

namespace SeQrRecall.UnitTests.Notes;

public sealed class NotesServiceTests
{
    [Fact]
    public async Task Create_returns_a_draft_owned_by_the_current_user()
    {
        await using NotesServiceHarness harness = await NotesServiceHarness.CreateAsync();
        CreateNoteResponse created = await harness.Service.CreateAsync();

        Assert.Equal(ProcessingStatus.Draft, created.ProcessingStatus);
        Note stored = (await harness.Context.Notes.FindAsync(created.Id))!;
        Assert.Equal(harness.UserId, stored.UserId);
    }

    [Fact]
    public async Task Get_does_not_return_another_users_note()
    {
        await using NotesServiceHarness harness = await NotesServiceHarness.CreateAsync();
        Guid otherNoteId = Guid.NewGuid();
        harness.Context.Notes.Add(new Note
        {
            Id = otherNoteId,
            UserId = Guid.NewGuid(),
            ProcessingStatus = ProcessingStatus.Draft
        });
        await harness.Context.SaveChangesAsync();

        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAsync(otherNoteId));
    }

    [Fact]
    public async Task Upload_rejects_unsupported_types_and_oversize_files()
    {
        await using NotesServiceHarness harness = await NotesServiceHarness.CreateAsync();
        CreateNoteResponse created = await harness.Service.CreateAsync();
        await using MemoryStream content = new(new byte[] { 1, 2, 3 });

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "text/plain",
            "note.txt",
            durationSeconds: null,
            contentLength: 3));

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "audio/mpeg",
            "note.mp3",
            durationSeconds: null,
            contentLength: AudioUploadRules.MaxBytes + 1));
    }

    [Fact]
    public async Task Upload_uses_configured_audio_max_bytes()
    {
        await using NotesServiceHarness harness = await NotesServiceHarness.CreateAsync(new UploadsOptions
        {
            AudioMaxBytes = 4
        });
        CreateNoteResponse created = await harness.Service.CreateAsync();
        await using MemoryStream content = new(new byte[] { 1, 2, 3, 4, 5 });

        await Assert.ThrowsAsync<ValidationException>(() => harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "audio/mpeg",
            "note.mp3",
            durationSeconds: null,
            contentLength: 5));
    }

    [Fact]
    public async Task Upload_queues_processing_and_sets_uploaded()
    {
        await using NotesServiceHarness harness = await NotesServiceHarness.CreateAsync();
        CreateNoteResponse created = await harness.Service.CreateAsync();
        await using MemoryStream content = new(new byte[] { 1, 2, 3, 4 });

        ProcessingStatusDto status = await harness.Service.UploadAudioAsync(
            created.Id,
            content,
            "audio/mpeg",
            "note.mp3",
            durationSeconds: 12,
            contentLength: 4);

        Assert.Equal(ProcessingStatus.Uploaded, status.ProcessingStatus);
        Assert.Equal(created.Id, harness.Queue.LastNoteId);
        Note stored = (await harness.Context.Notes.FindAsync(created.Id))!;
        Assert.False(string.IsNullOrWhiteSpace(stored.AudioFileUrl));
        Assert.Equal(12, stored.DurationSeconds);
    }

    [Fact]
    public async Task List_omits_deleted_notes()
    {
        await using NotesServiceHarness harness = await NotesServiceHarness.CreateAsync();
        CreateNoteResponse created = await harness.Service.CreateAsync();
        await harness.Service.DeleteAsync(created.Id);

        PagedResult<NoteListDto> page = await harness.Service.ListAsync(new PagedRequest());
        Assert.Empty(page.Items);
        await Assert.ThrowsAsync<NotFoundException>(() => harness.Service.GetAsync(created.Id));
    }

    private sealed class NotesServiceHarness : IAsyncDisposable
    {
        private NotesServiceHarness(ApplicationDbContext context, Guid userId, UploadsOptions uploads)
        {
            Context = context;
            UserId = userId;
            Queue = new RecordingQueue();
            Files = new NoteProcessingServiceTests.MemoryFileStorage();
            Service = new NotesService(
                context,
                new StubCurrentUser(userId),
                Files,
                Queue,
                Options.Create(uploads),
                NullLogger<NotesService>.Instance);
        }

        public ApplicationDbContext Context { get; }

        public Guid UserId { get; }

        public NotesService Service { get; }

        public RecordingQueue Queue { get; }

        public IFileStorageService Files { get; }

        public static async Task<NotesServiceHarness> CreateAsync(UploadsOptions? uploads = null)
        {
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            ApplicationDbContext context = new(options);
            Guid userId = Guid.NewGuid();
            context.Users.Add(new User { Id = userId, FullName = "Owner", IsActive = true });
            await context.SaveChangesAsync();
            return new NotesServiceHarness(context, userId, uploads ?? new UploadsOptions());
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
        public Guid? LastNoteId { get; private set; }

        public ValueTask QueueNoteProcessingAsync(Guid noteId, CancellationToken cancellationToken = default)
        {
            LastNoteId = noteId;
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
            return ValueTask.CompletedTask;
        }
    }
}
