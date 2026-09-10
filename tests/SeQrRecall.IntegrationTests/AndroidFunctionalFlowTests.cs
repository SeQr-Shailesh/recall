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
public sealed class AndroidFunctionalFlowTests
{
    private readonly ApiWebApplicationFactory _factory;

    public AndroidFunctionalFlowTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_record_note_customer_and_conversation_match_android_api_paths()
    {
        HttpClient client = await AcceptanceSupport.CreateAuthedClientAsync(_factory, "android");

        HttpResponseMessage me = await client.GetAsync("/api/v1/users/me");
        me.EnsureSuccessStatusCode();
        ApiResponse<UserDto>? current =
            await me.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(current?.Data);
        Assert.False(current.Data.IsProfileComplete);

        HttpResponseMessage profile = await client.PutAsJsonAsync("/api/v1/users/me", new UpdateProfileRequest
        {
            FullName = "Rajesh Kumar",
            Email = current.Data.Email
        });
        profile.EnsureSuccessStatusCode();
        ApiResponse<UserDto>? updated =
            await profile.Content.ReadFromJsonAsync<ApiResponse<UserDto>>(AcceptanceSupport.JsonOptions);
        Assert.True(updated?.Data?.IsProfileComplete);

        HttpResponseMessage createNote = await client.PostAsync("/api/v1/notes", content: null);
        createNote.EnsureSuccessStatusCode();
        ApiResponse<CreateNoteResponse>? draft =
            await createNote.Content.ReadFromJsonAsync<ApiResponse<CreateNoteResponse>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(draft?.Data);
        Assert.Equal(ProcessingStatus.Draft, draft.Data.ProcessingStatus);
        Guid noteId = draft.Data.Id;

        using (MultipartFormDataContent audio = AcceptanceSupport.CreateAudioForm())
        {
            HttpResponseMessage upload = await client.PostAsync($"/api/v1/notes/{noteId}/audio", audio);
            upload.EnsureSuccessStatusCode();
            ApiResponse<ProcessingStatusDto>? uploaded =
                await upload.Content.ReadFromJsonAsync<ApiResponse<ProcessingStatusDto>>(AcceptanceSupport.JsonOptions);
            Assert.Equal(ProcessingStatus.Uploaded, uploaded?.Data?.ProcessingStatus);
        }

        ProcessingStatusDto noteStatus =
            await AcceptanceSupport.WaitForTerminalStatusAsync(client, $"/api/v1/notes/{noteId}/status");
        Assert.Equal(ProcessingStatus.Completed, noteStatus.ProcessingStatus);
        Assert.Null(noteStatus.ProcessingError);

        HttpResponseMessage noteDetails = await client.GetAsync($"/api/v1/notes/{noteId}");
        noteDetails.EnsureSuccessStatusCode();
        string noteJson = await noteDetails.Content.ReadAsStringAsync();
        AcceptanceSupport.AssertNoStoredFileLeak(noteJson);
        ApiResponse<NoteDetailsDto>? note =
            JsonSerializer.Deserialize<ApiResponse<NoteDetailsDto>>(noteJson, AcceptanceSupport.JsonOptions);
        Assert.True(note?.Data?.HasAudio);
        Assert.Contains("Rajesh", note!.Data!.Transcript, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(note.Data.Title));
        Assert.NotEmpty(note.Data.ActionItems);

        HttpResponseMessage notesList = await client.GetAsync("/api/v1/notes?pageNumber=1&pageSize=20&search=Rajesh");
        notesList.EnsureSuccessStatusCode();
        string notesListJson = await notesList.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"transcript\"", notesListJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<PagedResult<NoteListDto>>? notesPage =
            JsonSerializer.Deserialize<ApiResponse<PagedResult<NoteListDto>>>(notesListJson, AcceptanceSupport.JsonOptions);
        Assert.Contains(notesPage?.Data?.Items ?? [], item => item.Id == noteId);

        HttpResponseMessage createCustomer = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            Name = "Patel Traders",
            CompanyName = "Patel Co",
            Mobile = "9876543210"
        });
        createCustomer.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? customer =
            await createCustomer.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(customer?.Data);
        Guid customerId = customer.Data.Id;
        Assert.False(customer.Data.HasPhoto);

        using (MultipartFormDataContent photo = AcceptanceSupport.CreatePhotoForm())
        {
            HttpResponseMessage photoUpload = await client.PostAsync($"/api/v1/customers/{customerId}/photo", photo);
            photoUpload.EnsureSuccessStatusCode();
        }

        HttpResponseMessage customerPhoto = await client.GetAsync($"/api/v1/customers/{customerId}/photo");
        customerPhoto.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", customerPhoto.Content.Headers.ContentType?.MediaType);

        HttpResponseMessage customersList = await client.GetAsync("/api/v1/customers?pageNumber=1&pageSize=20&search=Patel");
        customersList.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<CustomerDto>>? customersPage =
            await customersList.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CustomerDto>>>(AcceptanceSupport.JsonOptions);
        Assert.Contains(customersPage?.Data?.Items ?? [], item => item.Id == customerId);

        HttpResponseMessage createInteraction = await client.PostAsJsonAsync(
            "/api/v1/customer-interactions",
            new CreateCustomerInteractionRequest { CustomerId = customerId });
        createInteraction.EnsureSuccessStatusCode();
        ApiResponse<CreateCustomerInteractionResponse>? interactionDraft =
            await createInteraction.Content.ReadFromJsonAsync<ApiResponse<CreateCustomerInteractionResponse>>(AcceptanceSupport.JsonOptions);
        Assert.NotNull(interactionDraft?.Data);
        Guid interactionId = interactionDraft.Data.Id;
        Assert.Equal(ProcessingStatus.Draft, interactionDraft.Data.ProcessingStatus);

        using (MultipartFormDataContent photo = AcceptanceSupport.CreatePhotoForm())
        {
            (await client.PostAsync($"/api/v1/customer-interactions/{interactionId}/photo", photo)).EnsureSuccessStatusCode();
        }

        using (MultipartFormDataContent audio = AcceptanceSupport.CreateAudioForm())
        {
            HttpResponseMessage upload = await client.PostAsync($"/api/v1/customer-interactions/{interactionId}/audio", audio);
            upload.EnsureSuccessStatusCode();
        }

        ProcessingStatusDto interactionStatus = await AcceptanceSupport.WaitForTerminalStatusAsync(
            client,
            $"/api/v1/customer-interactions/{interactionId}/status");
        Assert.Equal(ProcessingStatus.Completed, interactionStatus.ProcessingStatus);

        HttpResponseMessage timeline = await client.GetAsync(
            $"/api/v1/customers/{customerId}/interactions?pageNumber=1&pageSize=20&search=Rajesh");
        timeline.EnsureSuccessStatusCode();
        string timelineJson = await timeline.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"transcript\"", timelineJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<PagedResult<CustomerInteractionListDto>>? timelinePage =
            JsonSerializer.Deserialize<ApiResponse<PagedResult<CustomerInteractionListDto>>>(timelineJson, AcceptanceSupport.JsonOptions);
        Assert.Contains(timelinePage?.Data?.Items ?? [], item => item.Id == interactionId);

        HttpResponseMessage interactionDetails = await client.GetAsync($"/api/v1/customer-interactions/{interactionId}");
        interactionDetails.EnsureSuccessStatusCode();
        string interactionJson = await interactionDetails.Content.ReadAsStringAsync();
        AcceptanceSupport.AssertNoStoredFileLeak(interactionJson);
        ApiResponse<CustomerInteractionDto>? interaction =
            JsonSerializer.Deserialize<ApiResponse<CustomerInteractionDto>>(interactionJson, AcceptanceSupport.JsonOptions);
        Assert.NotNull(interaction?.Data);
        Assert.True(interaction.Data.HasAudio);
        Assert.True(interaction.Data.HasPhoto);
        Assert.Equal("Patel Traders", interaction.Data.CustomerName);
        Assert.NotEmpty(interaction.Data.ActionItems);

        HttpResponseMessage interactionAudio = await client.GetAsync($"/api/v1/customer-interactions/{interactionId}/audio");
        interactionAudio.EnsureSuccessStatusCode();
        Assert.Equal("audio/mp4", interactionAudio.Content.Headers.ContentType?.MediaType);

        HttpResponseMessage interactionPhoto = await client.GetAsync($"/api/v1/customer-interactions/{interactionId}/photo");
        interactionPhoto.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", interactionPhoto.Content.Headers.ContentType?.MediaType);
    }
}
