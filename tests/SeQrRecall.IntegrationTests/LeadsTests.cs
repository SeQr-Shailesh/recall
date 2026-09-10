using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Auth;
using SeQrRecall.Application.Dtos.Leads;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Domain.Enums;
using SeQrRecall.Infrastructure.Authentication;
using Xunit;

namespace SeQrRecall.IntegrationTests;

[Collection("Api")]
public sealed class LeadsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiWebApplicationFactory _factory;

    public LeadsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Leads_require_authentication()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/leads");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_photo_audio_process_search_and_delete_succeed()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid leadId = await CreateLeadAsync(client);

        using MultipartFormDataContent photoForm = CreatePhotoForm();
        HttpResponseMessage photoResponse = await client.PostAsync($"/api/v1/leads/{leadId}/photo", photoForm);
        photoResponse.EnsureSuccessStatusCode();
        ApiResponse<LeadDetailsDto>? withPhoto =
            await photoResponse.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>(JsonOptions);
        Assert.NotNull(withPhoto?.Data);
        Assert.True(withPhoto.Data.HasPhoto);
        Assert.False(withPhoto.Data.HasAudio);
        Assert.Equal(ProcessingStatus.Draft, withPhoto.Data.ProcessingStatus);

        using MultipartFormDataContent audioForm = CreateAudioForm();
        HttpResponseMessage uploadResponse = await client.PostAsync($"/api/v1/leads/{leadId}/audio", audioForm);
        uploadResponse.EnsureSuccessStatusCode();
        ApiResponse<ProcessingStatusDto>? uploaded =
            await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<ProcessingStatusDto>>(JsonOptions);
        Assert.Equal(ProcessingStatus.Uploaded, uploaded?.Data?.ProcessingStatus);

        ProcessingStatusDto status = await WaitForTerminalStatusAsync(client, leadId);
        Assert.Equal(ProcessingStatus.Completed, status.ProcessingStatus);
        Assert.Null(status.ProcessingError);

        HttpResponseMessage detailsResponse = await client.GetAsync($"/api/v1/leads/{leadId}");
        detailsResponse.EnsureSuccessStatusCode();
        ApiResponse<LeadDetailsDto>? details =
            await detailsResponse.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>(JsonOptions);
        Assert.NotNull(details?.Data);
        Assert.True(details.Data.HasAudio);
        Assert.True(details.Data.HasPhoto);
        Assert.Contains("Rajesh", details.Data.Transcript, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(details.Data.Title));
        Assert.NotEmpty(details.Data.ActionItems);

        HttpResponseMessage listResponse = await client.GetAsync("/api/v1/leads?search=Rajesh");
        listResponse.EnsureSuccessStatusCode();
        string listJson = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"transcript\"", listJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<PagedResult<LeadListDto>>? list =
            JsonSerializer.Deserialize<ApiResponse<PagedResult<LeadListDto>>>(listJson, JsonOptions);
        LeadListDto listed = Assert.Single(list?.Data?.Items ?? [], item => item.Id == leadId);
        Assert.True(listed.HasPhoto);

        HttpResponseMessage photoStream = await client.GetAsync($"/api/v1/leads/{leadId}/photo");
        photoStream.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", photoStream.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await photoStream.Content.ReadAsByteArrayAsync());

        HttpResponseMessage audioStream = await client.GetAsync($"/api/v1/leads/{leadId}/audio");
        audioStream.EnsureSuccessStatusCode();
        Assert.Equal("audio/mpeg", audioStream.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await audioStream.Content.ReadAsByteArrayAsync());

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/v1/leads/{leadId}");
        deleteResponse.EnsureSuccessStatusCode();
        HttpResponseMessage afterDelete = await client.GetAsync($"/api/v1/leads/{leadId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Photo_can_be_attached_after_processing_completes()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid leadId = await CreateLeadAsync(client);

        using MultipartFormDataContent audioForm = CreateAudioForm();
        HttpResponseMessage uploadResponse = await client.PostAsync($"/api/v1/leads/{leadId}/audio", audioForm);
        uploadResponse.EnsureSuccessStatusCode();
        ProcessingStatusDto status = await WaitForTerminalStatusAsync(client, leadId);
        Assert.Equal(ProcessingStatus.Completed, status.ProcessingStatus);

        using MultipartFormDataContent photoForm = CreatePhotoForm();
        HttpResponseMessage photoResponse = await client.PostAsync($"/api/v1/leads/{leadId}/photo", photoForm);
        photoResponse.EnsureSuccessStatusCode();
        ApiResponse<LeadDetailsDto>? updated =
            await photoResponse.Content.ReadFromJsonAsync<ApiResponse<LeadDetailsDto>>(JsonOptions);
        Assert.NotNull(updated?.Data);
        Assert.True(updated.Data.HasPhoto);
        Assert.Equal(ProcessingStatus.Completed, updated.Data.ProcessingStatus);
    }

    [Fact]
    public async Task User_cannot_read_another_users_lead()
    {
        HttpClient owner = await CreateAuthedClientAsync();
        Guid leadId = await CreateLeadAsync(owner);

        using MultipartFormDataContent ownerPhoto = CreatePhotoForm();
        HttpResponseMessage attached = await owner.PostAsync($"/api/v1/leads/{leadId}/photo", ownerPhoto);
        attached.EnsureSuccessStatusCode();

        HttpClient other = await CreateAuthedClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/leads/{leadId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/leads/{leadId}/status")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/leads/{leadId}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/leads/{leadId}/photo")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/leads/{leadId}")).StatusCode);

        using MultipartFormDataContent audioForm = CreateAudioForm();
        HttpResponseMessage upload = await other.PostAsync($"/api/v1/leads/{leadId}/audio", audioForm);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);

        using MultipartFormDataContent otherPhoto = CreatePhotoForm();
        HttpResponseMessage photo = await other.PostAsync($"/api/v1/leads/{leadId}/photo", otherPhoto);
        Assert.Equal(HttpStatusCode.NotFound, photo.StatusCode);
    }

    [Fact]
    public async Task Unsupported_photo_is_rejected()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid leadId = await CreateLeadAsync(client);

        using MultipartFormDataContent form = new();
        ByteArrayContent file = new("not a photo"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "lead.txt");

        HttpResponseMessage upload = await client.PostAsync($"/api/v1/leads/{leadId}/photo", form);
        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        string email = $"leads-{Guid.NewGuid():N}@example.test";
        await client.PostAsJsonAsync("/api/v1/auth/send-otp", new SendOtpRequest { Email = email });
        HttpResponseMessage verify = await client.PostAsJsonAsync("/api/v1/auth/verify-otp", new VerifyOtpRequest
        {
            Email = email,
            Otp = DummyOtpService.AcceptedCode
        });
        verify.EnsureSuccessStatusCode();
        ApiResponse<VerifyOtpResponse>? body =
            await verify.Content.ReadFromJsonAsync<ApiResponse<VerifyOtpResponse>>(JsonOptions);
        Assert.NotNull(body?.Data?.Tokens.AccessToken);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", body.Data.Tokens.AccessToken);
        return client;
    }

    private static async Task<Guid> CreateLeadAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync("/api/v1/leads", content: null);
        response.EnsureSuccessStatusCode();
        ApiResponse<CreateLeadResponse>? created =
            await response.Content.ReadFromJsonAsync<ApiResponse<CreateLeadResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);
        Assert.Equal(ProcessingStatus.Draft, created.Data.ProcessingStatus);
        return created.Data.Id;
    }

    private static MultipartFormDataContent CreateAudioForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent audio = new([1, 2, 3, 4, 5]);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio, "file", "lead.mp3");
        form.Add(new StringContent("8"), "durationSeconds");
        return form;
    }

    private static MultipartFormDataContent CreatePhotoForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent photo = new([1, 2, 3, 4, 5, 6]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(photo, "file", "lead.jpg");
        return form;
    }

    private static async Task<ProcessingStatusDto> WaitForTerminalStatusAsync(HttpClient client, Guid leadId)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            HttpResponseMessage response = await client.GetAsync($"/api/v1/leads/{leadId}/status");
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

        throw new InvalidOperationException("Timed out waiting for lead processing.");
    }
}
