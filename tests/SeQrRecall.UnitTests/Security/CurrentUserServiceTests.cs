using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SeQrRecall.Infrastructure.Security;
using Xunit;

namespace SeQrRecall.UnitTests.Security;

public sealed class CurrentUserServiceTests
{
    [Fact]
    public void UserId_comes_from_NameIdentifier_claim()
    {
        Guid userId = Guid.NewGuid();
        CurrentUserService service = Create(principal: Authenticated(userId), headers: new HeaderDictionary
        {
            ["X-User-Id"] = Guid.NewGuid().ToString()
        });

        Assert.True(service.IsAuthenticated);
        Assert.Equal(userId, service.UserId);
    }

    [Fact]
    public void UserId_falls_back_to_sub_claim()
    {
        Guid userId = Guid.NewGuid();
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim("sub", userId.ToString())],
            authenticationType: "Bearer"));

        CurrentUserService service = Create(principal);

        Assert.True(service.IsAuthenticated);
        Assert.Equal(userId, service.UserId);
    }

    [Fact]
    public void Client_user_id_header_is_not_trusted()
    {
        CurrentUserService service = Create(
            principal: new ClaimsPrincipal(new ClaimsIdentity()),
            headers: new HeaderDictionary { ["X-User-Id"] = Guid.NewGuid().ToString() });

        Assert.False(service.IsAuthenticated);
        Assert.Throws<InvalidOperationException>(() => service.UserId);
    }

    [Fact]
    public void Unauthenticated_request_has_no_user_id()
    {
        CurrentUserService service = Create(principal: new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(service.IsAuthenticated);
        Assert.Throws<InvalidOperationException>(() => service.UserId);
    }

    private static CurrentUserService Create(ClaimsPrincipal principal, IHeaderDictionary? headers = null)
    {
        DefaultHttpContext http = new() { User = principal };
        if (headers is not null)
        {
            foreach (var header in headers)
            {
                http.Request.Headers[header.Key] = header.Value;
            }
        }

        return new CurrentUserService(new HttpContextAccessor { HttpContext = http });
    }

    private static ClaimsPrincipal Authenticated(Guid userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
            authenticationType: "Bearer"));
    }
}
