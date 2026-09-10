using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeQrRecall.Application.Abstractions.Ai;
using SeQrRecall.Infrastructure;
using SeQrRecall.Infrastructure.Ai;
using Xunit;

namespace SeQrRecall.UnitTests.Ai;

public sealed class AiProviderRegistrationTests
{
    [Fact]
    public void Mock_provider_registers_labeled_mock()
    {
        using ServiceProvider provider = Build("Mock", null);
        Assert.IsType<MockAiSummaryService>(provider.GetRequiredService<IAiSummaryService>());
    }

    [Fact]
    public void OpenAI_provider_registers_openai_service_when_api_key_is_set()
    {
        using ServiceProvider provider = Build("OpenAI", "not-a-real-key");
        Assert.IsType<OpenAiSummaryService>(provider.GetRequiredService<IAiSummaryService>());
    }

    [Fact]
    public void OpenAI_provider_without_api_key_fails_at_startup()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Build("OpenAI", ""));
        Assert.Contains("AI:ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gemini_provider_registers_gemini_service_when_api_key_is_set()
    {
        using ServiceProvider provider = Build("Gemini", "not-a-real-key");
        Assert.IsType<GeminiSummaryService>(provider.GetRequiredService<IAiSummaryService>());
    }

    [Fact]
    public void Gemini_provider_without_api_key_fails_at_startup()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Build("Gemini", ""));
        Assert.Contains("AI:ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Gemini_nested_api_key_registers_without_flat_key()
    {
        using ServiceProvider provider = BuildNested("Gemini", "AI:Gemini:ApiKey", "nested-gemini-key");
        Assert.IsType<GeminiSummaryService>(provider.GetRequiredService<IAiSummaryService>());
    }

    [Fact]
    public void OpenAI_nested_api_key_registers_without_flat_key()
    {
        using ServiceProvider provider = BuildNested("OpenAI", "AI:OpenAI:ApiKey", "nested-openai-key");
        Assert.IsType<OpenAiSummaryService>(provider.GetRequiredService<IAiSummaryService>());
    }

    [Fact]
    public void Unknown_provider_fails_at_startup()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Build("Anthropic", "x"));
        Assert.Contains("Anthropic", exception.Message, StringComparison.Ordinal);
        Assert.Contains("OpenAI", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Gemini", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider Build(string provider, string? apiKey)
    {
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Database=SeQrRecall;Trusted_Connection=True;TrustServerCertificate=True",
            ["Speech:Provider"] = "Mock",
            ["AI:Provider"] = provider,
            ["AI:ApiKey"] = apiKey,
            ["AI:BaseUrl"] = "https://api.openai.com/v1",
            ["AI:Model"] = "gpt-4o-mini",
            ["Storage:Provider"] = "Local",
            ["Storage:RootPath"] = Path.GetTempPath(),
            ["Otp:Provider"] = "Dummy"
        };

        ConfigurationManager configuration = new();
        foreach (KeyValuePair<string, string?> pair in values)
        {
            if (pair.Value is not null)
            {
                configuration[pair.Key] = pair.Value;
            }
        }

        ServiceCollection services = new();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    private static ServiceProvider BuildNested(string provider, string keyPath, string apiKey)
    {
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Database=SeQrRecall;Trusted_Connection=True;TrustServerCertificate=True",
            ["Speech:Provider"] = "Mock",
            ["AI:Provider"] = provider,
            [keyPath] = apiKey,
            ["Storage:Provider"] = "Local",
            ["Storage:RootPath"] = Path.GetTempPath(),
            ["Otp:Provider"] = "Dummy"
        };

        ConfigurationManager configuration = new();
        foreach (KeyValuePair<string, string?> pair in values)
        {
            configuration[pair.Key] = pair.Value;
        }

        ServiceCollection services = new();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }
}
