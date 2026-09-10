namespace SeQrRecall.Application.Dtos.Notes;

public sealed class NoteAudioStream
{
    public required Stream Stream { get; init; }

    public required string ContentType { get; init; }

    public required string FileName { get; init; }
}
