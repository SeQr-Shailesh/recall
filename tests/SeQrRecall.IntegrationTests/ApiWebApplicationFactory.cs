using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace SeQrRecall.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The Development configuration points Speech and AI at live third-party APIs, which reject
        // the synthetic audio these tests upload and leave every recording in Failed. Processing
        // assertions need the deterministic providers.
        builder.UseSetting("Speech:Provider", "Mock");
        builder.UseSetting("AI:Provider", "Mock");
    }
}

[CollectionDefinition("Api")]
public sealed class ApiCollection : ICollectionFixture<ApiWebApplicationFactory>
{
}
