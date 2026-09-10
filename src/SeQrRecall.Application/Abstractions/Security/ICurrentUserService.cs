namespace SeQrRecall.Application.Abstractions.Security;

/// <summary>
/// Resolves the authenticated user from JWT claims. Never trust a UserId sent by the client.
/// </summary>
public interface ICurrentUserService
{
    Guid UserId { get; }

    bool IsAuthenticated { get; }
}
