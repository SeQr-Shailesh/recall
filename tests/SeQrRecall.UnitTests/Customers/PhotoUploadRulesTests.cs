using SeQrRecall.Application.Customers;
using Xunit;

namespace SeQrRecall.UnitTests.Customers;

public sealed class PhotoUploadRulesTests
{
    [Theory]
    [InlineData("image/jpeg", true)]
    [InlineData("image/jpg", true)]
    [InlineData("image/png", true)]
    [InlineData("image/webp", true)]
    [InlineData("image/jpeg; charset=binary", true)]
    [InlineData("text/plain", false)]
    [InlineData("application/octet-stream", false)]
    [InlineData("", false)]
    public void Allowed_content_types_are_validated(string contentType, bool expected)
    {
        Assert.Equal(expected, PhotoUploadRules.IsAllowedContentType(contentType));
    }

    [Fact]
    public void Octet_stream_uses_the_original_file_extension()
    {
        string resolved = PhotoUploadRules.ResolveContentType("application/octet-stream", "face.jpg");
        Assert.Equal("image/jpeg", resolved);
        Assert.True(PhotoUploadRules.IsAllowedContentType(resolved));
    }

    [Fact]
    public void Image_jpg_content_type_is_normalized_to_jpeg()
    {
        Assert.Equal("image/jpeg", PhotoUploadRules.ResolveContentType("image/jpg", "face.jpg"));
    }

    [Fact]
    public void Stored_file_extension_maps_to_a_content_type()
    {
        Assert.Equal("image/jpeg", PhotoUploadRules.ContentTypeFromStoredFileName($"{Guid.NewGuid():N}.jpg"));
        Assert.Equal("image/png", PhotoUploadRules.ContentTypeFromStoredFileName($"{Guid.NewGuid():N}.png"));
    }

    [Fact]
    public void Default_max_bytes_is_5_mb()
    {
        Assert.Equal(5 * 1024 * 1024, PhotoUploadRules.DefaultMaxBytes);
        Assert.Equal(PhotoUploadRules.DefaultMaxBytes, PhotoUploadRules.MaxBytes);
    }
}
