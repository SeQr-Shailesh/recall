using System.Text.Json;
using System.Text.Json.Serialization;
using SeQrRecall.Application.Common.Models;
using Xunit;

namespace SeQrRecall.UnitTests.Contracts;

public sealed class ApiResponseContractTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    [Fact]
    public void Success_envelope_matches_v1_contract()
    {
        ApiResponse<Dictionary<string, string>> response = ApiResponse<Dictionary<string, string>>.Ok(
            new Dictionary<string, string>(),
            message: null);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonElement root = document.RootElement;

        Assert.True(root.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("message").ValueKind);
        Assert.False(root.TryGetProperty("errors", out _));
    }

    [Fact]
    public void Error_envelope_matches_v1_contract()
    {
        ApiResponse<object?> response = ApiResponse.Fail("Unable to process request.");

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(response, SerializerOptions));
        JsonElement root = document.RootElement;

        Assert.False(root.GetProperty("success").GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
        Assert.Equal("Unable to process request.", root.GetProperty("message").GetString());

        JsonElement errors = root.GetProperty("errors");
        Assert.Equal(JsonValueKind.Array, errors.ValueKind);
        Assert.Equal(0, errors.GetArrayLength());
    }
}
