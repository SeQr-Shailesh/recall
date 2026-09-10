using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Infrastructure.Storage;
using Xunit;

namespace SeQrRecall.UnitTests.Storage;

public sealed class LocalFileStorageServiceTests
{
    [Fact]
    public async Task Save_uses_generated_file_name_not_client_name()
    {
        string root = CreateTempRoot();
        LocalFileStorageService storage = Create(root);
        await using MemoryStream content = new("audio"u8.ToArray());

        StoredFile stored = await storage.SaveAsync(content, "audio/m4a", FileCategory.NoteAudio);

        Assert.False(stored.StoredFileName.Contains("client", StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(".m4a", stored.StoredFileName, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Audio/Notes/", stored.RelativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(root, stored.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public async Task OpenRead_rejects_path_traversal()
    {
        string root = CreateTempRoot();
        LocalFileStorageService storage = Create(root);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.OpenReadAsync("..\\secret.txt", FileCategory.NoteAudio));
    }

    [Fact]
    public async Task Round_trip_read_matches_written_bytes()
    {
        string root = CreateTempRoot();
        LocalFileStorageService storage = Create(root);
        byte[] payload = [1, 2, 3, 4, 5];
        await using MemoryStream content = new(payload);

        StoredFile stored = await storage.SaveAsync(content, "image/jpeg", FileCategory.ProfilePhoto);
        await using (Stream read = await storage.OpenReadAsync(stored.StoredFileName, FileCategory.ProfilePhoto))
        {
            using MemoryStream buffer = new();
            await read.CopyToAsync(buffer);
            Assert.Equal(payload, buffer.ToArray());
        }

        await storage.DeleteAsync(stored.StoredFileName, FileCategory.ProfilePhoto);
        Assert.False(File.Exists(Path.Combine(root, stored.RelativePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static LocalFileStorageService Create(string root)
    {
        return new LocalFileStorageService(Options.Create(new StorageOptions
        {
            Provider = "Local",
            RootPath = root
        }));
    }

    private static string CreateTempRoot()
    {
        string path = Path.Combine(Path.GetTempPath(), "SeQrRecallTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
