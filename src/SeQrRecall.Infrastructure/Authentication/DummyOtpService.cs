using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SeQrRecall.Application.Abstractions.Authentication;

namespace SeQrRecall.Infrastructure.Authentication;

public sealed class DummyOtpService : IOtpService
{
    public const string AcceptedCode = "123456";

    private readonly IHostEnvironment _environment;
    private readonly ILogger<DummyOtpService> _logger;

    public DummyOtpService(IHostEnvironment environment, ILogger<DummyOtpService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public Task SendOtpAsync(string destination, OtpChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        cancellationToken.ThrowIfCancellationRequested();

        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("Dummy OTP generated for {Channel}. Code: {Otp}", channel, AcceptedCode);
        }
        else
        {
            _logger.LogInformation("Dummy OTP dispatched for {Channel}", channel);
        }

        return Task.CompletedTask;
    }

    public Task<bool> ValidateOtpAsync(string destination, string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(string.Equals(code, AcceptedCode, StringComparison.Ordinal));
    }
}
