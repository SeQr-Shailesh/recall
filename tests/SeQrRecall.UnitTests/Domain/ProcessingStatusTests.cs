using SeQrRecall.Domain.Enums;
using Xunit;

namespace SeQrRecall.UnitTests.Domain;

public sealed class ProcessingStatusTests
{
    [Fact]
    public void Processing_status_contains_exactly_the_six_v1_values()
    {
        string[] names = Enum.GetNames<ProcessingStatus>();

        Assert.Equal(
            ["Draft", "Uploading", "Uploaded", "Processing", "Completed", "Failed"],
            names);
    }
}
