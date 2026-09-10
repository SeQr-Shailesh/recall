using SeQrRecall.Application.Notes;
using Xunit;

namespace SeQrRecall.UnitTests.Notes;

public sealed class AudioUploadRulesTests
{
    [Theory]
    [InlineData("audio/mpeg", true)]
    [InlineData("audio/mp4", true)]
    [InlineData("audio/mp4; codecs=mp4a.40.2", true)]
    [InlineData("audio/wav", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void Allowed_content_types_are_validated(string contentType, bool expected)
    {
        Assert.Equal(expected, AudioUploadRules.IsAllowedContentType(contentType));
    }

    [Fact]
    public void Octet_stream_uses_the_original_file_extension()
    {
        string resolved = AudioUploadRules.ResolveContentType("application/octet-stream", "clip.m4a");
        Assert.Equal("audio/mp4", resolved);
        Assert.True(AudioUploadRules.IsAllowedContentType(resolved));
    }

    [Fact]
    public void Stored_file_extension_maps_to_a_content_type()
    {
        Assert.Equal("audio/mpeg", AudioUploadRules.ContentTypeFromStoredFileName($"{Guid.NewGuid():N}.mp3"));
    }

    [Fact]
    public void Default_max_bytes_is_25_mb()
    {
        Assert.Equal(25 * 1024 * 1024, AudioUploadRules.DefaultMaxBytes);
        Assert.Equal(AudioUploadRules.DefaultMaxBytes, AudioUploadRules.MaxBytes);
    }
}
