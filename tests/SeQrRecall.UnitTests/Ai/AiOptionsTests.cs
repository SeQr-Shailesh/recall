using SeQrRecall.Application.Configuration;
using Xunit;

namespace SeQrRecall.UnitTests.Ai;

public sealed class AiOptionsTests
{
    [Fact]
    public void Resolve_uses_gemini_section_when_provider_is_gemini()
    {
        AiOptions options = new()
        {
            Provider = "Gemini",
            ApiKey = "flat-key",
            Model = "gpt-4o-mini",
            Gemini =
            {
                ApiKey = "gemini-key",
                Model = "gemini-3.5-flash",
                BaseUrl = "https://generativelanguage.googleapis.com/v1beta"
            }
        };

        AiProviderEndpointOptions endpoint = options.ResolveActiveEndpoint();

        Assert.Equal("gemini-key", endpoint.ApiKey);
        Assert.Equal("gemini-3.5-flash", endpoint.Model);
        Assert.Contains("generativelanguage", endpoint.BaseUrl, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_falls_back_to_flat_key_when_nested_key_is_empty()
    {
        AiOptions options = new()
        {
            Provider = "Gemini",
            ApiKey = "secret-from-user-secrets",
            Gemini = { Model = "gemini-3.5-flash" }
        };

        Assert.Equal("secret-from-user-secrets", options.ResolveActiveEndpoint().ApiKey);
    }
}
