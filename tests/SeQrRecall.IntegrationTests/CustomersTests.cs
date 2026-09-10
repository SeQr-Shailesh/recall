using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using SeQrRecall.Application.Common.Models;
using SeQrRecall.Application.Dtos.Auth;
using SeQrRecall.Application.Dtos.Customers;
using SeQrRecall.Infrastructure.Authentication;
using Xunit;

namespace SeQrRecall.IntegrationTests;

[Collection("Api")]
public sealed class CustomersTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ApiWebApplicationFactory _factory;

    public CustomersTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Customers_require_authentication()
    {
        HttpClient client = _factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_list_search_update_photo_and_delete_succeed()
    {
        HttpClient client = await CreateAuthedClientAsync();
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            Name = "Rajesh Patel",
            CompanyName = "Patel Traders",
            Mobile = "9876543210",
            Email = "rajesh@example.test"
        });
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOptions);
        Assert.NotNull(created?.Data);
        Guid customerId = created.Data.Id;
        Assert.Equal("Rajesh Patel", created.Data.Name);
        Assert.False(created.Data.HasPhoto);

        using MultipartFormDataContent form = CreatePhotoForm();
        HttpResponseMessage uploadResponse = await client.PostAsync($"/api/v1/customers/{customerId}/photo", form);
        uploadResponse.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? uploaded =
            await uploadResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOptions);
        Assert.True(uploaded?.Data?.HasPhoto);

        HttpResponseMessage detailsResponse = await client.GetAsync($"/api/v1/customers/{customerId}");
        detailsResponse.EnsureSuccessStatusCode();
        string detailsJson = await detailsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("photoUrl", detailsJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<CustomerDto>? details =
            JsonSerializer.Deserialize<ApiResponse<CustomerDto>>(detailsJson, JsonOptions);
        Assert.NotNull(details?.Data);
        Assert.True(details.Data.HasPhoto);

        HttpResponseMessage listResponse = await client.GetAsync("/api/v1/customers?search=Patel");
        listResponse.EnsureSuccessStatusCode();
        string listJson = await listResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"photoUrl\"", listJson, StringComparison.OrdinalIgnoreCase);
        ApiResponse<PagedResult<CustomerDto>>? list =
            JsonSerializer.Deserialize<ApiResponse<PagedResult<CustomerDto>>>(listJson, JsonOptions);
        Assert.Contains(list?.Data?.Items ?? [], item => item.Id == customerId);

        HttpResponseMessage miss = await client.GetAsync("/api/v1/customers?search=zzzz-not-a-match");
        miss.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<CustomerDto>>? empty =
            await miss.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CustomerDto>>>(JsonOptions);
        Assert.DoesNotContain(empty?.Data?.Items ?? [], item => item.Id == customerId);

        HttpResponseMessage photoResponse = await client.GetAsync($"/api/v1/customers/{customerId}/photo");
        photoResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/jpeg", photoResponse.Content.Headers.ContentType?.MediaType);
        byte[] bytes = await photoResponse.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);

        HttpResponseMessage updateResponse = await client.PutAsJsonAsync($"/api/v1/customers/{customerId}", new UpdateCustomerRequest
        {
            Name = "Rajesh P.",
            CompanyName = "Patel Traders",
            Mobile = "9876543210",
            Email = "rajesh@example.test"
        });
        updateResponse.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? updated =
            await updateResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOptions);
        Assert.Equal("Rajesh P.", updated?.Data?.Name);

        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/api/v1/customers/{customerId}");
        deleteResponse.EnsureSuccessStatusCode();
        HttpResponseMessage afterDelete = await client.GetAsync($"/api/v1/customers/{customerId}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task User_cannot_read_another_users_customer()
    {
        HttpClient owner = await CreateAuthedClientAsync();
        HttpResponseMessage createResponse = await owner.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            Name = "Owned"
        });
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOptions);
        Assert.NotNull(created?.Data);

        HttpClient other = await CreateAuthedClientAsync();
        HttpResponseMessage get = await other.GetAsync($"/api/v1/customers/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);

        using MultipartFormDataContent form = CreatePhotoForm();
        HttpResponseMessage upload = await other.PostAsync($"/api/v1/customers/{created.Data.Id}/photo", form);
        Assert.Equal(HttpStatusCode.NotFound, upload.StatusCode);

        HttpResponseMessage photo = await other.GetAsync($"/api/v1/customers/{created.Data.Id}/photo");
        Assert.Equal(HttpStatusCode.NotFound, photo.StatusCode);

        HttpResponseMessage update = await other.PutAsJsonAsync($"/api/v1/customers/{created.Data.Id}", new UpdateCustomerRequest
        {
            Name = "Hijack"
        });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);

        HttpResponseMessage delete = await other.DeleteAsync($"/api/v1/customers/{created.Data.Id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Unsupported_photo_is_rejected()
    {
        HttpClient client = await CreateAuthedClientAsync();
        HttpResponseMessage createResponse = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            Name = "Rajesh"
        });
        createResponse.EnsureSuccessStatusCode();
        ApiResponse<CustomerDto>? created =
            await createResponse.Content.ReadFromJsonAsync<ApiResponse<CustomerDto>>(JsonOptions);
        Assert.NotNull(created?.Data);

        using MultipartFormDataContent form = new();
        ByteArrayContent file = new("not a photo"u8.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file, "file", "photo.txt");

        HttpResponseMessage upload = await client.PostAsync($"/api/v1/customers/{created.Data.Id}/photo", form);
        Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
    }

    [Fact]
    public async Task Blank_name_is_rejected()
    {
        HttpClient client = await CreateAuthedClientAsync();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/customers", new CreateCustomerRequest
        {
            Name = "  "
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pagination_returns_owned_customers_newest_first()
    {
        HttpClient client = await CreateAuthedClientAsync();
        Guid first = await CreateCustomerAsync(client, "First Co");
        Guid second = await CreateCustomerAsync(client, "Second Co");
        Guid third = await CreateCustomerAsync(client, "Third Co");

        HttpResponseMessage page1 = await client.GetAsync("/api/v1/customers?pageNumber=1&pageSize=2");
        page1.EnsureSuccessStatusCode();
        ApiResponse<PagedResult<CustomerDto>>? body =
            await page1.Content.ReadFromJsonAsync<ApiResponse<PagedResult<CustomerDto>>>(JsonOptions);
        Assert.NotNull(body?.Data);
        Assert.Equal(2, body.Data.PageSize);
        Assert.True(body.Data.TotalCount >= 3);
        Assert.Equal(2, body.Data.Items.Count);
        Assert.Contains(body.Data.Items, item => item.Id == third || item.Id == second);

        _ = first;
    }

    private async Task<HttpClient> CreateAuthedClientAsync()
    {
        HttpClient client = _factory.CreateClient();
        string email = $"customers-{Guid.NewGuid():N}@example.test";
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

    private static MultipartFormDataContent CreatePhotoForm()
    {
        MultipartFormDataContent form = new();
        ByteArrayContent photo = new([1, 2, 3, 4, 5]);
        photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(photo, "file", "customer.jpg");
        return form;
    }
}
