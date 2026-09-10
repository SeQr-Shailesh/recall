using SeQrRecall.Application.Dtos.Auth;
using SeQrRecall.Application.Dtos.Users;

namespace SeQrRecall.Application.Abstractions.Authentication;

public interface IAuthenticationService
{
    Task SendOtpAsync(SendOtpRequest request, CancellationToken cancellationToken = default);

    Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

    Task<AuthTokensDto> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);

    Task<UserDto> UpdateCurrentUserAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default);
}
