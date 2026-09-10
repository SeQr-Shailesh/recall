using SeQrRecall.Application.Dtos.Users;

namespace SeQrRecall.Application.Dtos.Auth;

public sealed class AuthTokensDto
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTimeOffset AccessTokenExpiresOn { get; init; }
}

public sealed class VerifyOtpResponse
{
    public required AuthTokensDto Tokens { get; init; }

    public required UserDto User { get; init; }

    public required bool IsNewUser { get; init; }
}
