using System.Text.Json;
using SeQrRecall.Application.Common.Models;
using Xunit;

namespace SeQrRecall.UnitTests.Contracts;

public sealed class PagedResultContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Pagination_payload_matches_v1_contract()
    {
        PagedResult<object> page = PagedResult<object>.Create(
            items: Array.Empty<object>(),
            pageNumber: 1,
            pageSize: 20,
            totalCount: 100);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(page, SerializerOptions));
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.GetProperty("items").ValueKind);
        Assert.Equal(0, root.GetProperty("items").GetArrayLength());
        Assert.Equal(1, root.GetProperty("pageNumber").GetInt32());
        Assert.Equal(20, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(100, root.GetProperty("totalCount").GetInt32());
        Assert.Equal(5, root.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public void Paged_request_clamps_page_size_to_maximum_of_100()
    {
        var request = new PagedRequest
        {
            PageNumber = 0,
            PageSize = 500
        };

        Assert.Equal(PagedRequest.DefaultPageNumber, request.PageNumber);
        Assert.Equal(PagedRequest.MaxPageSize, request.PageSize);
    }
}
