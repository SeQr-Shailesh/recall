using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Ai;
using SeQrRecall.Application.Abstractions.Authentication;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Processing;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Abstractions.Speech;
using SeQrRecall.Application.Abstractions.Storage;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Infrastructure.Ai;
using SeQrRecall.Infrastructure.Authentication;
using SeQrRecall.Infrastructure.Persistence;
using SeQrRecall.Infrastructure.Processing;
using SeQrRecall.Infrastructure.Security;
using SeQrRecall.Infrastructure.Speech;
using SeQrRecall.Infrastructure.Storage;

namespace SeQrRecall.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
        }

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        string otpProvider = configuration[$"{OtpOptions.SectionName}:Provider"] ?? "Dummy";
        if (!string.Equals(otpProvider, "Dummy", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"OTP provider '{otpProvider}' is not implemented. Use 'Dummy' or add a provider in a later phase.");
        }

        services.AddSingleton<IOtpService, DummyOtpService>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
        {
            options.UseSqlServer(connectionString);
        });
        services.AddScoped<IApplicationDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<ApplicationDbContext>());

        string storageProvider = configuration[$"{StorageOptions.SectionName}:Provider"] ?? "Local";
        if (!string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Storage provider '{storageProvider}' is not implemented. Use 'Local' or add a provider in a later phase.");
        }

        services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        RegisterSpeechProvider(services, configuration);
        RegisterAiProvider(services, configuration);

        services.AddSingleton<InProcessBackgroundJobQueue>();
        services.AddSingleton<IBackgroundJobQueue>(static provider =>
            provider.GetRequiredService<InProcessBackgroundJobQueue>());
        services.AddHostedService<BackgroundJobWorker>();

        return services;
    }

    private static void RegisterSpeechProvider(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SpeechOptions>(configuration.GetSection(SpeechOptions.SectionName));

        string provider = configuration[$"{SpeechOptions.SectionName}:Provider"] ?? "Mock";
        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<ISpeechToTextService, MockSpeechToTextService>();
            return;
        }

        if (string.Equals(provider, "Sarvam", StringComparison.OrdinalIgnoreCase))
        {
            string apiKey = configuration[$"{SpeechOptions.SectionName}:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException(
                    "Speech:ApiKey is required when Speech:Provider is Sarvam.");
            }

            services.AddHttpClient<ISpeechToTextService, SarvamSpeechToTextService>((providerServices, client) =>
            {
                SpeechOptions options = providerServices.GetRequiredService<IOptions<SpeechOptions>>().Value;
                client.BaseAddress = new Uri(NormalizeSarvamBaseUrl(options.BaseUrl), UriKind.Absolute);
                int timeoutSeconds = options.TimeoutSeconds > 0 ? options.TimeoutSeconds : 120;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            return;
        }

        throw new InvalidOperationException(
            $"Speech provider '{provider}' is not implemented. Use 'Mock' or 'Sarvam'.");
    }

    private static string NormalizeSarvamBaseUrl(string? baseUrl)
    {
        string value = string.IsNullOrWhiteSpace(baseUrl)
            ? SarvamSpeechToTextService.DefaultBaseUrl
            : baseUrl.Trim();
        return value.EndsWith('/') ? value : value + "/";
    }

    private static void RegisterAiProvider(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        string provider = configuration[$"{AiOptions.SectionName}:Provider"] ?? "Mock";
        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAiSummaryService, MockAiSummaryService>();
            return;
        }

        if (string.Equals(provider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(ReadAiApiKey(configuration, "OpenAI")))
            {
                throw new InvalidOperationException(
                    "AI:OpenAI:ApiKey (or AI:ApiKey) is required when AI:Provider is OpenAI.");
            }

            services.AddHttpClient<IAiSummaryService, OpenAiSummaryService>((providerServices, client) =>
            {
                AiProviderEndpointOptions endpoint =
                    providerServices.GetRequiredService<IOptions<AiOptions>>().Value.ResolveActiveEndpoint();
                client.BaseAddress = new Uri(NormalizeOpenAiBaseUrl(endpoint.BaseUrl), UriKind.Absolute);
                int timeoutSeconds = endpoint.TimeoutSeconds > 0 ? endpoint.TimeoutSeconds : 60;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            return;
        }

        if (string.Equals(provider, "Gemini", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(ReadAiApiKey(configuration, "Gemini")))
            {
                throw new InvalidOperationException(
                    "AI:Gemini:ApiKey (or AI:ApiKey) is required when AI:Provider is Gemini.");
            }

            services.AddHttpClient<IAiSummaryService, GeminiSummaryService>((providerServices, client) =>
            {
                AiProviderEndpointOptions endpoint =
                    providerServices.GetRequiredService<IOptions<AiOptions>>().Value.ResolveActiveEndpoint();
                client.BaseAddress = new Uri(NormalizeGeminiBaseUrl(endpoint.BaseUrl), UriKind.Absolute);
                int timeoutSeconds = endpoint.TimeoutSeconds > 0 ? endpoint.TimeoutSeconds : 60;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            return;
        }

        if (string.Equals(provider, "Qwen", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IAiSummaryService, QwenSummaryService>((providerServices, client) =>
            {
                AiProviderEndpointOptions endpoint =
                    providerServices.GetRequiredService<IOptions<AiOptions>>().Value.ResolveActiveEndpoint();
                client.BaseAddress = new Uri(NormalizeQwenBaseUrl(endpoint.BaseUrl), UriKind.Absolute);
                int timeoutSeconds = endpoint.TimeoutSeconds > 0 ? endpoint.TimeoutSeconds : 120;
                client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            });
            return;
        }

        throw new InvalidOperationException(
            $"AI provider '{provider}' is not implemented. Use 'Mock', 'OpenAI', 'Gemini', or 'Qwen'.");
    }

    private static string? ReadAiApiKey(IConfiguration configuration, string providerName)
    {
        string? nested = configuration[$"{AiOptions.SectionName}:{providerName}:ApiKey"];
        if (!string.IsNullOrWhiteSpace(nested))
        {
            return nested;
        }

        return configuration[$"{AiOptions.SectionName}:ApiKey"];
    }

    private static string NormalizeOpenAiBaseUrl(string? baseUrl)
    {
        string value = string.IsNullOrWhiteSpace(baseUrl)
            ? OpenAiSummaryService.DefaultBaseUrl
            : baseUrl.Trim();
        return value.EndsWith('/') ? value : value + "/";
    }

    private static string NormalizeGeminiBaseUrl(string? baseUrl)
    {
        string value = string.IsNullOrWhiteSpace(baseUrl)
            ? GeminiSummaryService.DefaultBaseUrl
            : baseUrl.Trim();
        if (value.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
        {
            value = GeminiSummaryService.DefaultBaseUrl;
        }

        return value.EndsWith('/') ? value : value + "/";
    }

    private static string NormalizeQwenBaseUrl(string? baseUrl)
    {
        string value = string.IsNullOrWhiteSpace(baseUrl)
            ? QwenSummaryService.DefaultBaseUrl
            : baseUrl.Trim();
        if (value.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase)
            || value.Contains("generativelanguage.googleapis.com", StringComparison.OrdinalIgnoreCase))
        {
            value = QwenSummaryService.DefaultBaseUrl;
        }

        return value.EndsWith('/') ? value : value + "/";
    }
}
