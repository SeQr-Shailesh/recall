using SeQrRecall.Application.Configuration;
using Xunit;

namespace SeQrRecall.IntegrationTests;

/// <summary>
/// Phase 0 confirms the API host assembly and configuration contracts exist.
/// HTTP and database tests begin in Phase 1.
/// </summary>
public sealed class HostAssemblyTests
{
    [Fact]
    public void Api_host_assembly_is_loadable()
    {
        Type programType = typeof(Program);
        Assert.Equal("SeQrRecall.Api", programType.Assembly.GetName().Name);
    }

    [Fact]
    public void Configuration_section_names_match_appsettings_contract()
    {
        Assert.Equal("Jwt", JwtOptions.SectionName);
        Assert.Equal("Speech", SpeechOptions.SectionName);
        Assert.Equal("AI", AiOptions.SectionName);
        Assert.Equal("Storage", StorageOptions.SectionName);
        Assert.Equal("Otp", OtpOptions.SectionName);
        Assert.Equal("Uploads", UploadsOptions.SectionName);
        Assert.Equal("RateLimiting", RateLimitOptions.SectionName);
    }
}
