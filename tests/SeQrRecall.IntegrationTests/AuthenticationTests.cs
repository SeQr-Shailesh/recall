using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Auth;
using SeQrRecall.Application.Dtos.Users;
using SeQrRecall.Infrastructure.Authentication;
using Xunit;

namespace SeQrRecall.IntegrationTests;

[Collection("Api")]
public sealed class AuthenticationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ApiWebApplicationFactory _factory;

    public AuthenticationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Send_otp_without_identifier_returns_400()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/send-otp", new SendOtpRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Send_otp_appears_successful()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/auth/send-otp",
            new SendOtpRequest { Email = UniqueEmail() });
        response.EnsureSuccessStatusCode();

        ApiResponse<object>? body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Equal("A verification code has been sent.", body.Message);
    }

    [Fact]
    public async Task Invalid_otp_is_rejected()
    {
        HttpClient client = _factory.CreateClient();
        string email = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/auth/send-otp", new SendOtpRequest { Email = email });
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest
        {
            Email = email,
            Otp = "000000"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Exception", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_otp_is_rejected()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new { email = UniqueEmail() });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_login_profile_refresh_and_logout_succeed()
    {
        HttpClient client = _factory.CreateClient();
        string email = UniqueEmail();
        await client.PostAsJsonAsync("/api/v1/auth/send-otp", new SendOtpRequest { Email = email });

        HttpResponseMessage verifyResponse = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest
        {
            Email = email,
            Otp = DummyOtpService.AcceptedCode,
            DeviceInfo = "integration-test"
        });
        verifyResponse.EnsureSuccessStatusCode();

        ApiResponse<VerifyOtpResponse>? verify = await verifyResponse.Content.ReadFromJsonAsync<ApiResponse<VerifyOtpResponse>>(JsonOptions);
        Assert.NotNull(verify?.Data);
        Assert.True(verify.Data.IsNewUser);
        Assert.False(verify.Data.User.IsProfileComplete);
        Assert.False(string.IsNullOrWhiteSpace(verify.Data.Tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(verify.Data.Tokens.RefreshToken));

        HttpClient authed = CreateAuthedClient(verify.Data.Tokens.AccessToken);
        HttpResponseMessage meResponse = await authed.GetAsync("/api/v1/users/me");
        meResponse.EnsureSuccessStatusCode();
        ApiResponse<UserDto>? me = await meResponse.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(JsonOptions);
        Assert.Equal(verify.Data.User.Id, me?.Data?.Id);
        Assert.Equal(email, me?.Data?.Email);

        HttpResponseMessage updateResponse = await authed.PutAsJsonAsync("/api/v1/users/me", new UpdateProfileRequest
        {
            FullName = "Rajesh Kumar",
            Email = email
        });
        updateResponse.EnsureSuccessStatusCode();
        ApiResponse<UserDto>? updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(JsonOptions);
        Assert.True(updated?.Data?.IsProfileComplete);
        Assert.Equal("Rajesh Kumar", updated?.Data?.FullName);

        HttpResponseMessage refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = verify.Data.Tokens.RefreshToken
        });
        refreshResponse.EnsureSuccessStatusCode();
        ApiResponse<AuthTokensDto>? refreshed = await refreshResponse.Content.ReadFromJsonAsync<ApiResponse<AuthTokensDto>>(JsonOptions);
        Assert.NotNull(refreshed?.Data?.AccessToken);
        Assert.NotEqual(verify.Data.Tokens.RefreshToken, refreshed.Data.RefreshToken);

        HttpResponseMessage reuse = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = verify.Data.Tokens.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        HttpClient afterRefresh = CreateAuthedClient(refreshed.Data.AccessToken);
        HttpResponseMessage logoutResponse = await afterRefresh.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest
        {
            RefreshToken = refreshed.Data.RefreshToken
        });
        logoutResponse.EnsureSuccessStatusCode();

        HttpResponseMessage afterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = refreshed.Data.RefreshToken
        });
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Users_me_without_token_returns_401()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Authentication is required.", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_jwt_returns_401()
    {
        HttpClient client = CreateAuthedClient("not-a-jwt");
        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Second_login_is_not_a_new_user()
    {
        HttpClient client = _factory.CreateClient();
        string mobile = UniqueMobile();
        await client.PostAsJsonAsync("/api/v1/auth/send-otp", new SendOtpRequest { Mobile = mobile });
        VerifyOtpResponse first = await VerifyAsync(client, mobile, email: null);
        Assert.True(first.IsNewUser);

        VerifyOtpResponse second = await VerifyAsync(client, mobile, email: null);
        Assert.False(second.IsNewUser);
        Assert.Equal(first.User.Id, second.User.Id);
    }

    private static async Task<VerifyOtpResponse> VerifyAsync(HttpClient client, string? mobile, string? email)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest
        {
            Mobile = mobile,
            Email = email,
            Otp = DummyOtpService.AcceptedCode
        });
        response.EnsureSuccessStatusCode();
        ApiResponse<VerifyOtpResponse>? body = await response.Content.ReadFromJsonAsync<ApiResponse<VerifyOtpResponse>>(JsonOptions);
        Assert.NotNull(body?.Data);
        return body.Data;
    }

    private HttpClient CreateAuthedClient(string accessToken)
    {
        HttpClient client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.test";

    private static string UniqueMobile() => $"+1555{Random.Shared.Next(1000000, 9999999)}";
}
