using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Auth;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Domain.Enums;
using SeQrRecall.Infrastructure.Authentication;
using Xunit;

namespace SeQrRecall.IntegrationTests;

internal static class AcceptanceSupport
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<HttpClient> CreateAuthedClientAsync(
        ApiWebApplicationFactory factory,
        string prefix)
    {
        HttpClient client = factory.CreateClient();
        string email = $"{prefix}-{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/v1/auth/send-otp", new SendOtpRequest { Email = email });
        HttpResponseMessage verify = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest
        {
            Email = email,
            Otp = DummyOtpService.AcceptedCode,
            DeviceInfo = "SeQrRecall.android"
        });
        verify.EnsureSuccessStatusCode();
        ApiResponse<VerifyOtpResponse>? body =
            await verify.Content.ReadFromJsonAsync<ApiResponse<VerifyOtpResponse>>(JsonOptions);
        Assert.NotNull(body?.Data?.Tokens.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Data.Tokens.AccessToken);
        return client;
    }

    public static MultipartFormDataContent CreateAudioForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent audio = new([1, 2, 3, 4, 5]);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/mp4");
        form.Add(audio, "file", "seqr-note.m4a");
        form.Add(new StringContent("8"), "durationSeconds");
        return form;
    }

    public static MultipartFormDataContent CreatePhotoForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent photo = new([1, 2, 3, 4, 5]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(photo, "file", "customer.jpg");
        return form;
    }

    public static async Task<ProcessingStatusDto> WaitForTerminalStatusAsync(HttpClient client, string statusPath)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            HttpResponseMessage response = await client.GetAsync(statusPath);
            response.EnsureSuccessStatusCode();
            ApiResponse<ProcessingStatusDto>? body =
                await response.Content.ReadFromJsonAsync<ApiResponse<ProcessingStatusDto>>(JsonOptions);
            Assert.NotNull(body?.Data);
            if (body.Data.ProcessingStatus is ProcessingStatus.Completed or ProcessingStatus.Failed)
            {
                return body.Data;
            }

            await Task.Delay(100);
        }

        throw new InvalidOperationException($"Timed out waiting for {statusPath}.");
    }

    public static void AssertNoStoredFileLeak(string json)
    {
        Assert.DoesNotContain("photoUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audioFileUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SeQrRecallData", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Audio/Notes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Photos/Customers", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Photos/Interactions", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Audio\\Notes", json, StringComparison.OrdinalIgnoreCase);
    }
}
