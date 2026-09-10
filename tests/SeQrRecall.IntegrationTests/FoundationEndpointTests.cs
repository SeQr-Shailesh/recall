using System.Net.Http.Json;
using System.Text.Json;
using SeQrRecall.Application.Common.Constants;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.System;
using Xunit;

namespace SeQrRecall.IntegrationTests;

[Collection("Api")]
public sealed class FoundationEndpointTests
{
    private readonly ApiWebApplicationFactory _factory;

    public FoundationEndpointTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Health_returns_healthy_without_database_details()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health");
        string json = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Healthy", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ready_returns_healthy_when_database_is_reachable()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/health/ready");
        string json = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.Contains("Healthy", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DefaultConnection", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task System_version_returns_v1_envelope()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/system/version");
        response.EnsureSuccessStatusCode();

        ApiResponse<SystemVersionDto>? body = await response.Content.ReadFromJsonAsync<ApiResponse<SystemVersionDto>>(
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Equal("1.0", body.Data?.ApiVersion);
        Assert.False(string.IsNullOrWhiteSpace(body.Data?.ApplicationVersion));
        Assert.False(string.IsNullOrWhiteSpace(body.Data?.Environment));
        Assert.DoesNotContain("Secret", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Swagger_json_is_available_in_development()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/swagger/v1/swagger.json");
        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync();
        Assert.Contains("SeQr Recall API", json, StringComparison.Ordinal);
        Assert.Contains("/api/v1/system/version", json, StringComparison.Ordinal);
        Assert.Contains("Bearer", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Responses_include_correlation_and_security_headers()
    {
        HttpClient client = _factory.CreateClient();
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/system/version");
        request.Headers.TryAddWithoutValidation(HttpHeaderNames.CorrelationId, "phase1-test");

        HttpResponseMessage response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("phase1-test", response.Headers.GetValues(HttpHeaderNames.CorrelationId).Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("camera=()", response.Headers.GetValues("Permissions-Policy").Single(), StringComparison.Ordinal);
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
        IEnumerable<string> cacheControl = response.Headers.TryGetValues("Cache-Control", out IEnumerable<string>? headerValues)
            ? headerValues
            : response.Content.Headers.GetValues("Cache-Control");
        Assert.Contains(cacheControl, value => value.Contains("no-store", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Development_cors_allows_browser_origins_and_exposes_correlation_id()
    {
        HttpClient client = _factory.CreateClient();
        HttpRequestMessage request = new(HttpMethod.Get, "/api/v1/system/version");
        request.Headers.TryAddWithoutValidation("Origin", "http://localhost:3000");

        HttpResponseMessage response = await client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out IEnumerable<string>? origins));
        Assert.Contains(origins, origin => origin is "*" or "http://localhost:3000");
        Assert.True(response.Headers.TryGetValues("Access-Control-Expose-Headers", out IEnumerable<string>? exposed));
        Assert.Contains(
            exposed,
            header => header.Contains(HttpHeaderNames.CorrelationId, StringComparison.OrdinalIgnoreCase));
    }
}
