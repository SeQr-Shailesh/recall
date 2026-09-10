using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Domain.Entities;
using SeQrRecall.Infrastructure.Security;
using Xunit;

namespace SeQrRecall.UnitTests.Authentication;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void Access_token_contains_user_id_claims()
    {
        JwtTokenService service = Create();
        User user = new() { Id = Guid.NewGuid(), FullName = "Test" };

        var result = service.CreateAccessToken(user);
        JwtSecurityToken jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.True(result.ExpiresOn > DateTimeOffset.UtcNow);
        Assert.Contains(jwt.Claims, claim => claim.Type is JwtRegisteredClaimNames.Sub or ClaimTypes.NameIdentifier
            && claim.Value == user.Id.ToString());
    }

    [Fact]
    public void Refresh_token_hash_is_deterministic_and_not_the_raw_value()
    {
        JwtTokenService service = Create();
        string raw = service.CreateRefreshTokenValue();
        string hash = service.HashRefreshToken(raw);

        Assert.NotEqual(raw, hash);
        Assert.Equal(hash, service.HashRefreshToken(raw));
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void Missing_secret_fails_at_construction()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new JwtTokenService(Options.Create(new JwtOptions { Secret = "short" })));
    }

    private static JwtTokenService Create()
    {
        return new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "SeQrRecall",
            Audience = "SeQrRecall.Mobile",
            Secret = "DEV_ONLY_SEQ_RECALL_JWT_SECRET_CHANGE_ME_32",
            AccessTokenMinutes = 30,
            RefreshTokenDays = 30
        }));
    }
}
