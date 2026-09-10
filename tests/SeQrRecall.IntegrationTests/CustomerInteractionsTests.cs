using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Auth;
using SeQrRecall.Application.Dtos.Customers;
using SeQrRecall.Application.Dtos.Interactions;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Domain.Enums;
using SeQrRecall.Infrastructure.Authentication;
using Xunit;

namespace SeQrRecall.IntegrationTests;

[Collection("Api")]
public sealed class CustomerInteractionsTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiWebApplicationFactory _factory;

    public CustomerInteractionsTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Interactions_require_authentication()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync($"/api/v1/customers/{Guid.NewGuid()}/interactions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_upload_process_timeline_photo_and_audio_succeed()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid customerId = await CreateCustomerAsync(client, "Rajesh Patel");

        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/customer-interactions",
            new CreateCustomerInteractionRequest { CustomerId = customerId });
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CreateCustomerInteractionResponse>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreateCustomerInteractionResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);
        Assert.Equal(ProcessingStatus.Draft, created.Data.ProcessingStatus);
        Guid interactionId = created.Data.Id;

        using MultipartFormDataContent photoForm = CreatePhotoForm();
        HttpResponseMessage photoUpload = await client.PostAsync(
            $"/api/v1/customer-interactions/{interactionId}/photo",
            photoForm);
        photoUpload.EnsureSuccessStatusCode();

        using MultipartFormDataContent audioForm = CreateAudioForm();
        HttpResponseMessage uploadResponse = await client.PostAsync(
            $"/api/v1/customer-interactions/{interactionId}/audio",
            audioForm);
        uploadResponse.EnsureSuccessStatusCode();
        ApiResponse<ProcessingStatusDto>? uploaded =
            await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<ProcessingStatusDto>>(JsonOptions);
        Assert.Equal(ProcessingStatus.Uploaded, uploaded?.Data?.ProcessingStatus);

        ProcessingStatusDto status = await WaitForTerminalStatusAsync(client, interactionId);
        Assert.Equal(ProcessingStatus.Completed, status.ProcessingStatus);
        Assert.Null(status.ProcessingError);

        HttpResponseMessage detailsResponse = await client.GetAsync($"/api/v1/customer-interactions/{interactionId}");
        detailsResponse.EnsureSuccessStatusCode();
        string detailsJson = await detailsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("photoUrl", detailsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("audioFileUrl", detailsJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<CustomerInteractionDto>? details =
            JsonSerializer.Deserialize<ApiResponse<CustomerInteractionDto>>(detailsJson, JsonOptions);
        Assert.NotNull(details?.Data);
        Assert.True(details.Data.HasAudio);
        Assert.True(details.Data.HasPhoto);
        Assert.Contains("Rajesh", details.Data.Transcript, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Rajesh Patel", details.Data.CustomerName);
        Assert.NotEmpty(details.Data.ActionItems);

        HttpResponseMessage listResponse = await client.GetAsync($"/api/v1/customers/{customerId}/interactions?search=Rajesh");
        listResponse.EnsureSuccessStatusCode();
        string listJson = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"transcript\"", listJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<PagedResult<CustomerInteractionListDto>>? list =
            JsonSerializer.Deserialize<ApiResponse<PagedResult<CustomerInteractionListDto>>>(listJson, JsonOptions);
        Assert.Contains(list?.Data?.Items ?? [], item => item.Id == interactionId);

        HttpResponseMessage audioResponse = await client.GetAsync($"/api/v1/customer-interactions/{interactionId}/audio");
        audioResponse.EnsureSuccessStatusCode();
        Assert.Equal("audio/mpeg", audioResponse.Content.Headers.ContentType?.MediaType);

        HttpResponseMessage photoResponse = await client.GetAsync($"/api/v1/customer-interactions/{interactionId}/photo");
        photoResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", photoResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task User_cannot_read_another_users_interaction()
    {
        HttpClient owner = await CreateAuthedClientAsync();
        Guid customerId = await CreateCustomerAsync(owner, "Owned");
        HttpResponseMessage createResponse = await owner.PostAsJsonAsync(
            "/api/v1/customer-interactions",
            new CreateCustomerInteractionRequest { CustomerId = customerId });
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CreateCustomerInteractionResponse>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreateCustomerInteractionResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);

        HttpClient other = await CreateAuthedClientAsync();
        HttpResponseMessage timeline = await other.GetAsync($"/api/v1/customers/{customerId}/interactions");
        Assert.Equal(HttpStatusCode.NotFound, timeline.StatusCode);

        HttpResponseMessage get = await other.GetAsync($"/api/v1/customer-interactions/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using MultipartFormDataContent audio = CreateAudioForm();
        HttpResponseMessage upload = await other.PostAsync($"/api/v1/customer-interactions/{created.Data.Id}/audio", audio);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);

        using MultipartFormDataContent photo = CreatePhotoForm();
        HttpResponseMessage photoUpload = await other.PostAsync(
            $"/api/v1/customer-interactions/{created.Data.Id}/photo",
            photo);
        Assert.Equal(HttpStatusCode.NotFound, photoUpload.StatusCode);

        HttpResponseMessage createOnOthersCustomer = await other.PostAsJsonAsync(
            "/api/v1/customer-interactions",
            new CreateCustomerInteractionRequest { CustomerId = customerId });
        Assert.Equal(HttpStatusCode.NotFound, createOnOthersCustomer.StatusCode);
    }

    [Fact]
    public async Task Unsupported_audio_is_rejected()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid customerId = await CreateCustomerAsync(client, "Rajesh");
        HttpResponseMessage createResponse = await client.PostAsJsonAsync(
            "/api/v1/customer-interactions",
            new CreateCustomerInteractionRequest { CustomerId = customerId });
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CreateCustomerInteractionResponse>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreateCustomerInteractionResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);

        using MultipartFormDataContent form = new();
        ByteArrayContent file = new("not audio"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "clip.txt");

        HttpResponseMessage upload = await client.PostAsync($"/api/v1/customer-interactions/{created.Data.Id}/audio", form);
        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        string email = $"interactions-{Guid.NewGuid():N}@example.test";
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

    private static async Task<Guid> CreateCustomerAsync(HttpClient client, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            Name = name
        });
        response.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? created =
            await response.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOptions);
        Assert.NotNull(created?.Data);
        return created.Data.Id;
    }

    private static MultipartFormDataContent CreateAudioForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent audio = new([1, 2, 3, 4, 5]);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio, "file", "clip.mp3");
        form.Add(new StringContent("8"), "durationSeconds");
        return form;
    }

    private static MultipartFormDataContent CreatePhotoForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent photo = new([1, 2, 3, 4, 5]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(photo, "file", "scene.jpg");
        return form;
    }

    private static async Task<ProcessingStatusDto> WaitForTerminalStatusAsync(HttpClient client, Guid interactionId)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            HttpResponseMessage response = await client.GetAsync($"/api/v1/customer-interactions/{interactionId}/status");
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

        throw new InvalidOperationException("Timed out waiting for interaction processing.");
    }
}
