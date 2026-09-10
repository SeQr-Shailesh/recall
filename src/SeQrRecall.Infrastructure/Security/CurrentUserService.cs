using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SeQrRecall.Application.Abstractions.Security;

namespace SeQrRecall.Infrastructure.Security;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true
        && TryGetUserId(out _);

    public Guid UserId
    {
        get
        {
            if (!TryGetUserId(out Guid userId))
            {
                throw new InvalidOperationException("The current request is not authenticated.");
            }

            return userId;
        }
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        ClaimsPrincipal? principal = _httpContextAccessor.HttpContext?.User;
        if (principal is null)
        {
            return false;
        }

        string? value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}
