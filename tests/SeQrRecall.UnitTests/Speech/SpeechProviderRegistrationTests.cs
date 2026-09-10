using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SeQrRecall.Application.Abstractions.Speech;
using SeQrRecall.Infrastructure;
using SeQrRecall.Infrastructure.Speech;
using Xunit;

namespace SeQrRecall.UnitTests.Speech;

public sealed class SpeechProviderRegistrationTests
{
    [Fact]
    public void Mock_provider_registers_labeled_mock()
    {
        using ServiceProvider provider = Build(["Mock", null]);
        ISpeechToTextService speech = provider.GetRequiredService<ISpeechToTextService>();
        Assert.IsType<MockSpeechToTextService>(speech);
    }

    [Fact]
    public void Sarvam_provider_registers_sarvam_service_when_api_key_is_set()
    {
        using ServiceProvider provider = Build(["Sarvam", "not-a-real-key"]);
        ISpeechToTextService speech = provider.GetRequiredService<ISpeechToTextService>();
        Assert.IsType<SarvamSpeechToTextService>(speech);
    }

    [Fact]
    public void Sarvam_provider_without_api_key_fails_at_startup()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Build(["Sarvam", ""]));
        Assert.Contains("Speech:ApiKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_provider_fails_at_startup()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Build(["Whisper", "x"]));
        Assert.Contains("Whisper", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Sarvam", exception.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider Build(string?[] speech)
    {
        string provider = speech[0] ?? "Mock";
        string? apiKey = speech[1];
        Dictionary<string, string?> values = new()
        {
            ["ConnectionStrings:DefaultConnection"] =
                "Server=localhost;Database=SeQrRecall;Trusted_Connection=True;TrustServerCertificate=True",
            ["Speech:Provider"] = provider,
            ["Speech:ApiKey"] = apiKey,
            ["Speech:BaseUrl"] = "https://api.sarvam.ai",
            ["AI:Provider"] = "Mock",
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
}
