using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SeQrRecall.Application.Abstractions.Authentication;
using SeQrRecall.Infrastructure.Authentication;
using Xunit;

namespace SeQrRecall.UnitTests.Authentication;

public sealed class DummyOtpServiceTests
{
    [Fact]
    public async Task Send_succeeds_without_throwing()
    {
        DummyOtpService service = Create(isDevelopment: true);
        await service.SendOtpAsync("+919999999999", OtpChannel.Sms);
    }

    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", false)]
    [InlineData("12345", false)]
    [InlineData("654321", false)]
    public async Task Only_123456_is_accepted(string code, bool expected)
    {
        DummyOtpService service = Create(isDevelopment: false);
        bool result = await service.ValidateOtpAsync("user@example.com", code);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Accepted_code_constant_is_123456()
    {
        Assert.Equal("123456", DummyOtpService.AcceptedCode);
    }

    private static DummyOtpService Create(bool isDevelopment)
    {
        return new DummyOtpService(new StubHostEnvironment(isDevelopment), NullLogger<DummyOtpService>.Instance);
    }

    private sealed class StubHostEnvironment : IHostEnvironment
    {
        public StubHostEnvironment(bool isDevelopment)
        {
            EnvironmentName = isDevelopment ? Environments.Development : Environments.Production;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "SeQrRecall.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
