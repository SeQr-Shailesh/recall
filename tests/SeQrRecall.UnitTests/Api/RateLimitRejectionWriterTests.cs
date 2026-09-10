using Microsoft.AspNetCore.Http;
using SeQrRecall.Api.RateLimiting;
using SeQrRecall.Application.Configuration;
using Xunit;

namespace SeQrRecall.UnitTests.Api;

public sealed class RateLimitRejectionWriterTests
{
    [Fact]
    public void Partition_key_prefers_the_authenticated_user()
    {
        DefaultHttpContext context = new();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "user-1")],
                "test"));

        Assert.Equal("user-1", RateLimitRejectionWriter.PartitionKey(context));
    }

    [Fact]
    public void Partition_key_falls_back_to_remote_ip()
    {
        DefaultHttpContext context = new();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.10");
        Assert.Equal("203.0.113.10", RateLimitRejectionWriter.PartitionKey(context));
    }

    [Fact]
    public void Normalize_clamps_invalid_limits()
    {
        RateLimitOptions normalized = RateLimitRejectionWriter.Normalize(new RateLimitOptions
        {
            OtpSendPermitLimit = 0,
            OtpVerifyPermitLimit = -3,
            OtpWindowSeconds = 0,
            UploadPermitLimit = 0,
            UploadWindowSeconds = -1
        });

        Assert.Equal(1, normalized.OtpSendPermitLimit);
        Assert.Equal(1, normalized.OtpVerifyPermitLimit);
        Assert.Equal(60, normalized.OtpWindowSeconds);
        Assert.Equal(1, normalized.UploadPermitLimit);
        Assert.Equal(60, normalized.UploadWindowSeconds);
    }
}
