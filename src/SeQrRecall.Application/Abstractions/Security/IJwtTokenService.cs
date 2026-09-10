using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Abstractions.Security;

public sealed class AccessTokenResult
{
    public required string AccessToken { get; init; }

    public required DateTimeOffset ExpiresOn { get; init; }
}

/// <summary>
/// Issues and hashes JWT access tokens and refresh tokens.
/// </summary>
public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(User user);

    string CreateRefreshTokenValue();

    string HashRefreshToken(string refreshToken);
}
