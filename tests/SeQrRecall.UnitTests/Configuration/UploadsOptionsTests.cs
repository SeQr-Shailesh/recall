using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Customers;
using SeQrRecall.Application.Notes;
using Xunit;

namespace SeQrRecall.UnitTests.Configuration;

public sealed class UploadsOptionsTests
{
    [Fact]
    public void Defaults_match_the_documented_caps()
    {
        UploadsOptions options = new();
        Assert.Equal(AudioUploadRules.DefaultMaxBytes, options.AudioMaxBytes);
        Assert.Equal(PhotoUploadRules.DefaultMaxBytes, options.PhotoMaxBytes);
        Assert.Equal(25 * 1024 * 1024, options.AudioMaxBytes);
        Assert.Equal(5 * 1024 * 1024, options.PhotoMaxBytes);
        Assert.True(options.MultipartBodyLimitBytes() <= UploadsOptions.HardCeilingBytes);
        Assert.True(options.MultipartBodyLimitBytes() > options.AudioMaxBytes);
    }

    [Fact]
    public void Multipart_limit_never_exceeds_the_hard_ceiling()
    {
        UploadsOptions options = new()
        {
            AudioMaxBytes = UploadsOptions.HardCeilingBytes,
            PhotoMaxBytes = UploadsOptions.HardCeilingBytes
        };

        Assert.Equal(UploadsOptions.HardCeilingBytes, options.MultipartBodyLimitBytes());
    }
}
