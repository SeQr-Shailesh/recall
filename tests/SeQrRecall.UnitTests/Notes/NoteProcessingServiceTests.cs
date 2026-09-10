using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeQrRecall.Application.Abstractions.Ai;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Speech;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Services;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Domain.Enums;
using SeQrRecall.Infrastructure.Ai;
using SeQrRecall.Infrastructure.Persistence;
using SeQrRecall.Infrastructure.Speech;
using Xunit;

namespace SeQrRecall.UnitTests.Notes;

public sealed class NoteProcessingServiceTests
{
    [Fact]
    public async Task Successful_stt_and_ai_complete_the_note()
    {
        await using NoteProcessingHarness harness = CreateHarness();
        Note note = await harness.SeedNoteAsync();

        await harness.Processor.ProcessNoteAsync(note.Id);

        Note updated = (await harness.Db.GetNoteWithActionItemsByIdAsync(note.Id))!;
        Assert.Equal(ProcessingStatus.Completed, updated.ProcessingStatus);
        Assert.Equal(MockSpeechToTextService.EnglishTranscript, updated.Transcript);
        Assert.False(string.IsNullOrWhiteSpace(updated.Title));
        Assert.Null(updated.ProcessingError);
        Assert.NotEmpty(updated.ActionItems);
        Assert.NotNull(updated.CompletedOn);
    }

    [Fact]
    public async Task Stt_failure_marks_the_note_failed()
    {
        await using NoteProcessingHarness harness = CreateHarness(speech: new FailingSpeech());
        Note note = await harness.SeedNoteAsync();

        await harness.Processor.ProcessNoteAsync(note.Id);

        Note updated = (await harness.Db.GetNoteByIdAsync(note.Id))!;
        Assert.Equal(ProcessingStatus.Failed, updated.ProcessingStatus);
        Assert.Null(updated.Transcript);
        Assert.Contains("Transcription failed", updated.ProcessingError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ai_failure_keeps_the_transcript_and_completes()
    {
        await using NoteProcessingHarness harness = CreateHarness(ai: new FailingAi());
        Note note = await harness.SeedNoteAsync();

        await harness.Processor.ProcessNoteAsync(note.Id);

        Note updated = (await harness.Db.GetNoteByIdAsync(note.Id))!;
        Assert.Equal(ProcessingStatus.Completed, updated.ProcessingStatus);
        Assert.Equal(MockSpeechToTextService.EnglishTranscript, updated.Transcript);
        Assert.Null(updated.Title);
        Assert.Contains("Summary could not be generated", updated.ProcessingError, StringComparison.Ordinal);
        Assert.NotNull(updated.CompletedOn);
    }

    [Fact]
    public async Task Successful_stt_and_ai_complete_the_interaction()
    {
        await using NoteProcessingHarness harness = CreateHarness();
        CustomerInteraction interaction = await harness.SeedInteractionAsync();

        await harness.Processor.ProcessCustomerInteractionAsync(interaction.Id);

        CustomerInteraction updated = (await harness.Db.GetCustomerInteractionWithActionItemsByIdAsync(interaction.Id))!;
        Assert.Equal(ProcessingStatus.Completed, updated.ProcessingStatus);
        Assert.Equal(MockSpeechToTextService.EnglishTranscript, updated.Transcript);
        Assert.False(string.IsNullOrWhiteSpace(updated.ShortSummary));
        Assert.Null(updated.ProcessingError);
        Assert.NotEmpty(updated.ActionItems);
        Assert.NotNull(updated.CompletedOn);
    }

    [Fact]
    public async Task Stt_failure_marks_the_interaction_failed()
    {
        await using NoteProcessingHarness harness = CreateHarness(speech: new FailingSpeech());
        CustomerInteraction interaction = await harness.SeedInteractionAsync();

        await harness.Processor.ProcessCustomerInteractionAsync(interaction.Id);

        CustomerInteraction updated = (await harness.Db.GetCustomerInteractionByIdAsync(interaction.Id))!;
        Assert.Equal(ProcessingStatus.Failed, updated.ProcessingStatus);
        Assert.Null(updated.Transcript);
        Assert.Contains("Transcription failed", updated.ProcessingError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ai_failure_keeps_the_interaction_transcript_and_completes()
    {
        await using NoteProcessingHarness harness = CreateHarness(ai: new FailingAi());
        CustomerInteraction interaction = await harness.SeedInteractionAsync();

        await harness.Processor.ProcessCustomerInteractionAsync(interaction.Id);

        CustomerInteraction updated = (await harness.Db.GetCustomerInteractionByIdAsync(interaction.Id))!;
        Assert.Equal(ProcessingStatus.Completed, updated.ProcessingStatus);
        Assert.Equal(MockSpeechToTextService.EnglishTranscript, updated.Transcript);
        Assert.Null(updated.ShortSummary);
        Assert.Contains("Summary could not be generated", updated.ProcessingError, StringComparison.Ordinal);
        Assert.NotNull(updated.CompletedOn);
    }

    private static NoteProcessingHarness CreateHarness(
        ISpeechToTextService? speech = null,
        IAiSummaryService? ai = null)
    {
        return new NoteProcessingHarness(
            speech ?? new MockSpeechToTextService(),
            ai ?? new MockAiSummaryService());
    }

    private sealed class NoteProcessingHarness : IAsyncDisposable
    {
        public NoteProcessingHarness(ISpeechToTextService speech, IAiSummaryService ai)
        {
            Files = new MemoryFileStorage();
            DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            Context = new ApplicationDbContext(options);
            Db = Context;
            Processor = new NoteProcessingService(
                Db,
                Files,
                speech,
                ai,
                NullLogger<NoteProcessingService>.Instance);
        }

        public ApplicationDbContext Context { get; }

        public IApplicationDbContext Db { get; }

        public MemoryFileStorage Files { get; }

        public NoteProcessingService Processor { get; }

        public async Task<Note> SeedNoteAsync()
        {
            User user = new() { Id = Guid.NewGuid(), FullName = "Tester", IsActive = true };
            await using MemoryStream audio = new(new byte[] { 1, 2, 3, 4 });
            StoredFile stored = await Files.SaveAsync(audio, "audio/mpeg", FileCategory.NoteAudio);
            Note note = new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                AudioFileUrl = stored.StoredFileName,
                ProcessingStatus = ProcessingStatus.Uploaded
            };
            Context.Users.Add(user);
            Context.Notes.Add(note);
            await Context.SaveChangesAsync();
            return note;
        }

        public async Task<CustomerInteraction> SeedInteractionAsync()
        {
            User user = new() { Id = Guid.NewGuid(), FullName = "Tester", IsActive = true };
            Customer customer = new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Name = "Rajesh Patel",
                IsActive = true
            };
            await using MemoryStream audio = new(new byte[] { 1, 2, 3, 4 });
            StoredFile stored = await Files.SaveAsync(audio, "audio/mpeg", FileCategory.InteractionAudio);
            CustomerInteraction interaction = new()
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                CustomerId = customer.Id,
                AudioFileUrl = stored.StoredFileName,
                InteractionDate = DateTimeOffset.UtcNow,
                ProcessingStatus = ProcessingStatus.Uploaded
            };
            Context.Users.Add(user);
            Context.Customers.Add(customer);
            Context.CustomerInteractions.Add(interaction);
            await Context.SaveChangesAsync();
            return interaction;
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
        }
    }

    private sealed class FailingSpeech : ISpeechToTextService
    {
        public Task<SpeechTranscriptionResult> TranscribeAsync(
            SpeechTranscriptionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new ExternalProviderException("Mock", "STT unavailable");
        }
    }

    private sealed class FailingAi : IAiSummaryService
    {
        public Task<AiSummaryResult> SummarizeAsync(string englishTranscript, CancellationToken cancellationToken = default)
        {
            throw new ExternalProviderException("Mock", "AI unavailable");
        }
    }

    internal sealed class MemoryFileStorage : IFileStorageService
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.OrdinalIgnoreCase);

        public async Task<StoredFile> SaveAsync(
            Stream content,
            string contentType,
            FileCategory category,
            CancellationToken cancellationToken = default)
        {
            await using MemoryStream copy = new();
            await content.CopyToAsync(copy, cancellationToken);
            string name = $"{Guid.NewGuid():N}.mp3";
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
            return Task.CompletedTask;
        }
    }
}
