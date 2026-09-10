using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Customers;
using SeQrRecall.Application.Dtos.Interactions;
using SeQrRecall.Application.Dtos.Notes;
using SeQrRecall.Application.Dtos.Users;
using SeQrRecall.Domain.Enums;
using Xunit;

namespace SeQrRecall.IntegrationTests;

[Collection("Api")]
public sealed class OwnershipIsolationTests
{
    private readonly ApiWebApplicationFactory _factory;

    public OwnershipIsolationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task User_cannot_read_or_mutate_another_users_notes_customers_interactions_or_files()
    {
        HttpClient owner = await AcceptanceSupport.CreateAuthedClientAsync(_factory, "owner");
        HttpClient other = await AcceptanceSupport.CreateAuthedClientAsync(_factory, "other");

        Guid ownerUserId = await ReadCurrentUserIdAsync(owner);
        string marker = $"IDOR-{Guid.NewGuid():N}";

        Guid noteId = await CreateProcessedNoteAsync(owner);
        Guid customerId = await CreateCustomerAsync(owner, marker);
        using (MultipartFormDataContent customerPhoto = AcceptanceSupport.CreatePhotoForm())
        {
            HttpResponseMessage photoUpload =
                await owner.PostAsync($"/api/v1/customers/{customerId}/photo", customerPhoto);
            photoUpload.EnsureSuccessStatusCode();
        }

        Guid interactionId = await CreateProcessedInteractionAsync(owner, customerId);

        await AssertNotFoundAsync(other.GetAsync($"/api/v1/notes/{noteId}"));
        await AssertNotFoundAsync(other.GetAsync($"/api/v1/notes/{noteId}/status"));
        await AssertNotFoundAsync(other.GetAsync($"/api/v1/notes/{noteId}/audio"));
        using (MultipartFormDataContent audio = AcceptanceSupport.CreateAudioForm())
        {
            await AssertNotFoundAsync(other.PostAsync($"/api/v1/notes/{noteId}/audio", audio));
        }

        await AssertNotFoundAsync(other.DeleteAsync($"/api/v1/notes/{noteId}"));

        HttpResponseMessage notesSearch = await other.GetAsync($"/api/v1/notes?search={marker}");
        notesSearch.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<NoteListDto>>? notesPage =
            await notesSearch.Content.ReadFromJsonAsync<ApiResponse<PagedResult<NoteListDto>>>(AcceptanceSupport.JsonOptions);
        Assert.DoesNotContain(notesPage?.Data?.Items ?? [], item => item.Id == noteId);

        HttpResponseMessage notesList = await other.GetAsync("/api/v1/notes?pageNumber=1&pageSize=100");
        notesList.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<NoteListDto>>? allNotes =
            await notesList.Content.ReadFromJsonAsync<ApiResponse<PagedResult<NoteListDto>>>(AcceptanceSupport.JsonOptions);
        Assert.DoesNotContain(allNotes?.Data?.Items ?? [], item => item.Id == noteId);

        await AssertNotFoundAsync(other.GetAsync($"/api/v1/customers/{customerId}"));
        await AssertNotFoundAsync(other.GetAsync($"/api/v1/customers/{customerId}/photo"));
        using (MultipartFormDataContent photo = AcceptanceSupport.CreatePhotoForm())
        {
            await AssertNotFoundAsync(other.PostAsync($"/api/v1/customers/{customerId}/photo", photo));
        }

        await AssertNotFoundAsync(other.PutAsJsonAsync($"/api/v1/customers/{customerId}", new UpdateCustomerRequest
        {
            Name = "Hijack"
        }));
        await AssertNotFoundAsync(other.DeleteAsync($"/api/v1/customers/{customerId}"));

        HttpResponseMessage customersSearch = await other.GetAsync($"/api/v1/customers?search={marker}");
        customersSearch.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<CustomerDto>>? customersPage =
            await customersSearch.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CustomerDto>>>(AcceptanceSupport.JsonOptions);
        Assert.DoesNotContain(customersPage?.Data?.Items ?? [], item => item.Id == customerId);

        await AssertNotFoundAsync(other.GetAsync($"/api/v1/customers/{customerId}/interactions"));
        await AssertNotFoundAsync(other.GetAsync($"/api/v1/customer-interactions/{interactionId}"));
        await AssertNotFoundAsync(other.GetAsync($"/api/v1/customer-interactions/{interactionId}/status"));
        await AssertNotFoundAsync(other.GetAsync($"/api/v1/customer-interactions/{interactionId}/audio"));
        await AssertNotFoundAsync(other.GetAsync($"/api/v1/customer-interactions/{interactionId}/photo"));
        using (MultipartFormDataContent audio = AcceptanceSupport.CreateAudioForm())
        {
            await AssertNotFoundAsync(other.PostAsync($"/api/v1/customer-interactions/{interactionId}/audio", audio));
        }

        using (MultipartFormDataContent photo = AcceptanceSupport.CreatePhotoForm())
        {
            await AssertNotFoundAsync(other.PostAsync($"/api/v1/customer-interactions/{interactionId}/photo", photo));
        }

        await AssertNotFoundAsync(other.PostAsJsonAsync(
            "/api/v1/customer-interactions",
            new CreateCustomerInteractionRequest { CustomerId = customerId }));

        HttpResponseMessage spoof = await other.PostAsJsonAsync("/api/v1/customers", new
        {
            name = "Spoofed",
            userId = ownerUserId
        });
        spoof.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? spoofed =
            await spoof.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(spoofed?.Data);
        await AssertNotFoundAsync(owner.GetAsync($"/api/v1/customers/{spoofed.Data.Id}"));
        HttpResponseMessage spoofGet = await other.GetAsync($"/api/v1/customers/{spoofed.Data.Id}");
        spoofGet.EnsureSuccessStatusCode();

        HttpResponseMessage profileSpoof = await other.PutAsJsonAsync("/api/v1/users/me", new
        {
            fullName = "Other User",
            id = ownerUserId
        });
        profileSpoof.EnsureSuccessStatusCode();
        ApiResponse<UserDto>? otherProfile =
            await profileSpoof.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(AcceptanceSupport.JsonOptions);
        Assert.NotEqual(ownerUserId, otherProfile?.Data?.Id);

        HttpResponseMessage ownerMe = await owner.GetAsync("/api/v1/users/me");
        ownerMe.EnsureSuccessStatusCode();
        ApiResponse<UserDto>? ownerProfile =
            await ownerMe.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(AcceptanceSupport.JsonOptions);
        Assert.Equal(ownerUserId, ownerProfile?.Data?.Id);
        Assert.NotEqual("Other User", ownerProfile?.Data?.FullName);
    }

    [Fact]
    public async Task Files_require_a_valid_jwt_and_are_not_served_as_static_paths()
    {
        HttpClient owner = await AcceptanceSupport.CreateAuthedClientAsync(_factory, "files");
        Guid noteId = await CreateProcessedNoteAsync(owner);
        Guid customerId = await CreateCustomerAsync(owner, "File Owner");
        using (MultipartFormDataContent photo = AcceptanceSupport.CreatePhotoForm())
        {
            (await owner.PostAsync($"/api/v1/customers/{customerId}/photo", photo)).EnsureSuccessStatusCode();
        }

        Guid interactionId = await CreateProcessedInteractionAsync(owner, customerId);

        HttpClient anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/notes/{noteId}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/customers/{customerId}/photo")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/customer-interactions/{interactionId}/audio")).StatusCode);
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/v1/customer-interactions/{interactionId}/photo")).StatusCode);

        HttpClient invalid = _factory.CreateClient();
        invalid.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");
        Assert.Equal(HttpStatusCode.Unauthorized, (await invalid.GetAsync($"/api/v1/notes/{noteId}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await invalid.GetAsync($"/api/v1/customers/{customerId}/photo")).StatusCode);

        HttpResponseMessage noteAudio = await owner.GetAsync($"/api/v1/notes/{noteId}/audio");
        noteAudio.EnsureSuccessStatusCode();
        Assert.DoesNotContain("filename", noteAudio.Content.Headers.ContentDisposition?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        HttpResponseMessage details = await owner.GetAsync($"/api/v1/notes/{noteId}");
        details.EnsureSuccessStatusCode();
        string noteJson = await details.Content.ReadAsStringAsync();
        AcceptanceSupport.AssertNoStoredFileLeak(noteJson);

        HttpResponseMessage customerDetails = await owner.GetAsync($"/api/v1/customers/{customerId}");
        customerDetails.EnsureSuccessStatusCode();
        AcceptanceSupport.AssertNoStoredFileLeak(await customerDetails.Content.ReadAsStringAsync());

        HttpResponseMessage interactionDetails = await owner.GetAsync($"/api/v1/customer-interactions/{interactionId}");
        interactionDetails.EnsureSuccessStatusCode();
        AcceptanceSupport.AssertNoStoredFileLeak(await interactionDetails.Content.ReadAsStringAsync());

        foreach (string path in new[]
        {
            "/Audio/Notes/missing.mp3",
            "/Photos/Customers/missing.jpg",
            "/Photos/Interactions/missing.jpg",
            "/Audio/Interactions/missing.m4a"
        })
        {
            HttpResponseMessage staticFile = await anonymous.GetAsync(path);
            Assert.NotEqual(HttpStatusCode.OK, staticFile.StatusCode);
        }
    }

    private static async Task AssertNotFoundAsync(Task<HttpResponseMessage> request)
    {
        HttpResponseMessage response = await request;
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        string json = await response.Content.ReadAsStringAsync();
        Assert.Contains("The requested resource was not found.", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", json, StringComparison.Ordinal);
    }

    private static async Task<Guid> ReadCurrentUserIdAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/api/v1/users/me");
        response.EnsureSuccessStatusCode();
        ApiResponse<UserDto>? body =
            await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(body?.Data);
        return body.Data.Id;
    }

    private static async Task<Guid> CreateProcessedNoteAsync(HttpClient client)
    {
        HttpResponseMessage create = await client.PostAsync("/api/v1/notes", content: null);
        create.EnsureSuccessStatusCode();
        ApiResponse<CreateNoteResponse>? created =
            await create.Content.ReadFromJsonAsync<ApiResponse<CreateNoteResponse>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(created?.Data);
        using MultipartFormDataContent audio = AcceptanceSupport.CreateAudioForm();
        (await client.PostAsync($"/api/v1/notes/{created.Data.Id}/audio", audio)).EnsureSuccessStatusCode();
        ProcessingStatusDto status =
            await AcceptanceSupport.WaitForTerminalStatusAsync(client, $"/api/v1/notes/{created.Data.Id}/status");
        Assert.Equal(ProcessingStatus.Completed, status.ProcessingStatus);
        return created.Data.Id;
    }

    private static async Task<Guid> CreateCustomerAsync(HttpClient client, string name)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            Name = name
        });
        response.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? created =
            await response.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(created?.Data);
        return created.Data.Id;
    }

    private static async Task<Guid> CreateProcessedInteractionAsync(HttpClient client, Guid customerId)
    {
        HttpResponseMessage create = await client.PostAsJsonAsync(
            "/api/v1/customer-interactions",
            new CreateCustomerInteractionRequest { CustomerId = customerId });
        create.EnsureSuccessStatusCode();
        ApiResponse<CreateCustomerInteractionResponse>? created =
            await create.Content.ReadFromJsonAsync<ApiResponse<CreateCustomerInteractionResponse>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(created?.Data);
        using (MultipartFormDataContent photo = AcceptanceSupport.CreatePhotoForm())
        {
            (await client.PostAsync($"/api/v1/customer-interactions/{created.Data.Id}/photo", photo)).EnsureSuccessStatusCode();
        }

        using (MultipartFormDataContent audio = AcceptanceSupport.CreateAudioForm())
        {
            (await client.PostAsync($"/api/v1/customer-interactions/{created.Data.Id}/audio", audio)).EnsureSuccessStatusCode();
        }

        ProcessingStatusDto status = await AcceptanceSupport.WaitForTerminalStatusAsync(
            client,
            $"/api/v1/customer-interactions/{created.Data.Id}/status");
        Assert.Equal(ProcessingStatus.Completed, status.ProcessingStatus);
        return created.Data.Id;
    }
}
