using System.Net;
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

[Collection("Api")]
public sealed class NotesTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiWebApplicationFactory _factory;

    public NotesTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Notes_require_authentication()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/notes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_upload_process_list_search_audio_and_delete_succeed()
    {
        HttpClient client = await CreateAuthedClientAsync();
        HttpResponseMessage createResponse = await client.PostAsync("/api/v1/notes", content: null);
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CreateNoteResponse>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreateNoteResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);
        Assert.Equal(ProcessingStatus.Draft, created.Data.ProcessingStatus);
        Guid noteId = created.Data.Id;

        using MultipartFormDataContent form = CreateAudioForm();
        HttpResponseMessage uploadResponse = await client.PostAsync($"/api/v1/notes/{noteId}/audio", form);
        uploadResponse.EnsureSuccessStatusCode();
        ApiResponse<ProcessingStatusDto>? uploaded =
            await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<ProcessingStatusDto>>(JsonOptions);
        Assert.Equal(ProcessingStatus.Uploaded, uploaded?.Data?.ProcessingStatus);

        ProcessingStatusDto status = await WaitForTerminalStatusAsync(client, noteId);
        Assert.Equal(ProcessingStatus.Completed, status.ProcessingStatus);
        Assert.Null(status.ProcessingError);

        HttpResponseMessage detailsResponse = await client.GetAsync($"/api/v1/notes/{noteId}");
        detailsResponse.EnsureSuccessStatusCode();
        ApiResponse<NoteDetailsDto>? details =
            await detailsResponse.Content.ReadFromJsonAsync<ApiResponse<NoteDetailsDto>>(JsonOptions);
        Assert.NotNull(details?.Data);
        Assert.True(details.Data.HasAudio);
        Assert.Contains("Rajesh", details.Data.Transcript, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(details.Data.Title));
        Assert.NotEmpty(details.Data.ActionItems);

        HttpResponseMessage listResponse = await client.GetAsync("/api/v1/notes?search=Rajesh");
        listResponse.EnsureSuccessStatusCode();
        string listJson = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"transcript\"", listJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<PagedResult<NoteListDto>>? list =
            JsonSerializer.Deserialize<ApiResponse<PagedResult<NoteListDto>>>(listJson, JsonOptions);
        Assert.Contains(list?.Data?.Items ?? [], item => item.Id == noteId);

        HttpResponseMessage miss = await client.GetAsync("/api/v1/notes?search=zzzz-not-a-match");
        miss.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<NoteListDto>>? empty =
            await miss.Content.ReadFromJsonAsync<ApiResponse<PagedResult<NoteListDto>>>(JsonOptions);
        Assert.DoesNotContain(empty?.Data?.Items ?? [], item => item.Id == noteId);

        HttpResponseMessage audioResponse = await client.GetAsync($"/api/v1/notes/{noteId}/audio");
        audioResponse.EnsureSuccessStatusCode();
        Assert.Equal("audio/mpeg", audioResponse.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await audioResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/v1/notes/{noteId}");
        deleteResponse.EnsureSuccessStatusCode();
        HttpResponseMessage afterDelete = await client.GetAsync($"/api/v1/notes/{noteId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task User_cannot_read_another_users_note()
    {
        HttpClient owner = await CreateAuthedClientAsync();
        HttpResponseMessage createResponse = await owner.PostAsync("/api/v1/notes", content: null);
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CreateNoteResponse>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreateNoteResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);

        HttpClient other = await CreateAuthedClientAsync();
        HttpResponseMessage get = await other.GetAsync($"/api/v1/notes/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using MultipartFormDataContent form = CreateAudioForm();
        HttpResponseMessage upload = await other.PostAsync($"/api/v1/notes/{created.Data.Id}/audio", form);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);

        HttpResponseMessage audio = await other.GetAsync($"/api/v1/notes/{created.Data.Id}/audio");
        Assert.Equal(HttpStatusCode.NotFound, audio.StatusCode);

        HttpResponseMessage delete = await other.DeleteAsync($"/api/v1/notes/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Unsupported_audio_is_rejected()
    {
        HttpClient client = await CreateAuthedClientAsync();
        HttpResponseMessage createResponse = await client.PostAsync("/api/v1/notes", content: null);
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CreateNoteResponse>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CreateNoteResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);

        using MultipartFormDataContent form = new();
        ByteArrayContent file = new("not audio"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "note.txt");

        HttpResponseMessage upload = await client.PostAsync($"/api/v1/notes/{created.Data.Id}/audio", form);
        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Pagination_returns_owned_notes_newest_first()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid first = await CreateNoteAsync(client);
        Guid second = await CreateNoteAsync(client);
        Guid third = await CreateNoteAsync(client);

        HttpResponseMessage page1 = await client.GetAsync("/api/v1/notes?pageNumber=1&pageSize=2");
        page1.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<NoteListDto>>? body =
            await page1.Content.ReadFromJsonAsync<ApiResponse<PagedResult<NoteListDto>>>(JsonOptions);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.PageSize);
        Assert.True(body.Data.TotalCount >= 3);
        Assert.Equal(2, body.Data.Items.Count);
        Assert.Contains(body.Data.Items, item => item.Id == third || item.Id == second);
        Assert.DoesNotContain("transcript", JsonSerializer.Serialize(body.Data.Items[0], JsonOptions), StringComparison.OrdinalIgnoreCase);

        _ = first;
    }

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        string email = $"notes-{Guid.NewGuid():N}@example.test";
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

    private static async Task<Guid> CreateNoteAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsync("/api/v1/notes", content: null);
        response.EnsureSuccessStatusCode();
        ApiResponse<CreateNoteResponse>? created =
            await response.Content.ReadFromJsonAsync<ApiResponse<CreateNoteResponse>>(JsonOptions);
        Assert.NotNull(created?.Data);
        return created.Data.Id;
    }

    private static MultipartFormDataContent CreateAudioForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent audio = new([1, 2, 3, 4, 5]);
        audio.Headers.ContentType = new MediaTypeHeaderValue("audio/mpeg");
        form.Add(audio, "file", "note.mp3");
        form.Add(new StringContent("8"), "durationSeconds");
        return form;
    }

    private static async Task<ProcessingStatusDto> WaitForTerminalStatusAsync(HttpClient client, Guid noteId)
    {
        for (int attempt = 0; attempt < 50; attempt++)
        {
            HttpResponseMessage response = await client.GetAsync($"/api/v1/notes/{noteId}/status");
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

        throw new InvalidOperationException("Timed out waiting for note processing.");
    }
}
