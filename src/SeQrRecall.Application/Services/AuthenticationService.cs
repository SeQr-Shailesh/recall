using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SeQrRecall.Application.Abstractions.Authentication;
using SeQrRecall.Application.Abstractions.Persistence;
using SeQrRecall.Application.Abstractions.Security;
using SeQrRecall.Application.Common.Exceptions;
using SeQrRecall.Application.Configuration;
using SeQrRecall.Application.Dtos.Auth;
using SeQrRecall.Application.Dtos.Users;
using SeQrRecall.Application.Mapping;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Services;

public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IApplicationDbContext _db;
    private readonly IOtpService _otpService;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ICurrentUserService _currentUser;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IApplicationDbContext db,
        IOtpService otpService,
        IJwtTokenService jwtTokenService,
        ICurrentUserService currentUser,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthenticationService> logger)
    {
        _db = db;
        _otpService = otpService;
        _jwtTokenService = jwtTokenService;
        _currentUser = currentUser;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task SendOtpAsync(SendOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        (string destination, OtpChannel channel) = ResolveDestination(request.Mobile, request.Email);
        await _otpService.SendOtpAsync(destination, channel, cancellationToken);
        _logger.LogInformation("OTP send requested for {Channel}", channel);
    }

    public async Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Otp))
        {
            throw new ValidationException("A verification code is required.", ["Otp is required."]);
        }

        (string destination, OtpChannel channel) = ResolveDestination(request.Mobile, request.Email);
        bool valid = await _otpService.ValidateOtpAsync(destination, request.Otp.Trim(), cancellationToken);
        if (!valid)
        {
            throw new ValidationException("The verification code is incorrect.");
        }

        User? user = channel == OtpChannel.Sms
            ? await _db.GetUserByMobileAsync(destination, cancellationToken)
            : await _db.GetUserByEmailAsync(destination, cancellationToken);

        bool isNewUser = user is null;
        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                FullName = string.Empty,
                Mobile = channel == OtpChannel.Sms ? destination : null,
                Email = channel == OtpChannel.Email ? destination : null,
                IsActive = true
            };
            _db.Add(user);
        }
        else if (!user.IsActive)
        {
            throw new ForbiddenAccessException("This account is not active.");
        }

        user.LastLoginOn = DateTimeOffset.UtcNow;
        AuthTokensDto tokens = await IssueTokensAsync(user, request.DeviceInfo);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {UserId} authenticated. NewUser: {IsNewUser}", user.Id, isNewUser);

        return new VerifyOtpResponse
        {
            Tokens = tokens,
            User = user.ToDto(),
            IsNewUser = isNewUser
        };
    }

    public async Task<AuthTokensDto> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        RefreshToken stored = await GetActiveRefreshTokenAsync(request.RefreshToken, cancellationToken);

        if (!stored.User.IsActive)
        {
            stored.RevokedOn = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            throw new ForbiddenAccessException("This account is not active.");
        }

        stored.RevokedOn = DateTimeOffset.UtcNow;
        AuthTokensDto tokens = await IssueTokensAsync(stored.User, request.DeviceInfo ?? stored.DeviceInfo);
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated for user {UserId}", stored.UserId);
        return tokens;
    }

    public async Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException();
        }

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new ValidationException("A refresh token is required.", ["RefreshToken is required."]);
        }

        string hash = _jwtTokenService.HashRefreshToken(request.RefreshToken);
        RefreshToken? stored = await _db.GetRefreshTokenByHashAsync(hash, cancellationToken);
        if (stored is not null && stored.UserId == _currentUser.UserId && stored.RevokedOn is null)
        {
            stored.RevokedOn = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("User {UserId} logged out", _currentUser.UserId);
    }

    public async Task<UserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        User user = await GetAuthenticatedUserAsync(cancellationToken);
        return user.ToDto();
    }

    public async Task<UserDto> UpdateCurrentUserAsync(UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            throw new ValidationException("Full name is required.", ["FullName is required."]);
        }

        User user = await GetAuthenticatedUserAsync(cancellationToken);
        string? mobile = NormalizeMobile(request.Mobile);
        string? email = NormalizeEmail(request.Email);

        if (mobile is null && email is null && user.Mobile is null && user.Email is null)
        {
            throw new ValidationException("A mobile number or email is required.");
        }

        if (mobile is not null && !string.Equals(mobile, user.Mobile, StringComparison.Ordinal))
        {
            User? existing = await _db.GetUserByMobileAsync(mobile, cancellationToken);
            if (existing is not null && existing.Id != user.Id)
            {
                throw new ValidationException("This mobile number is already in use.");
            }
        }

        if (email is not null && !string.Equals(email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            User? existing = await _db.GetUserByEmailAsync(email, cancellationToken);
            if (existing is not null && existing.Id != user.Id)
            {
                throw new ValidationException("This email is already in use.");
            }
        }

        user.FullName = request.FullName.Trim();
        if (mobile is not null)
        {
            user.Mobile = mobile;
        }

        if (email is not null)
        {
            user.Email = email;
        }

        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("User {UserId} updated profile", user.Id);
        return user.ToDto();
    }

    private async Task<User> GetAuthenticatedUserAsync(CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new UnauthorizedException();
        }

        User? user = await _db.GetUserByIdAsync(_currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedException();
        }

        return user;
    }

    private async Task<RefreshToken> GetActiveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException("The refresh token is invalid.");
        }

        string hash = _jwtTokenService.HashRefreshToken(refreshToken);
        RefreshToken? stored = await _db.GetRefreshTokenByHashAsync(hash, cancellationToken);
        if (stored is null)
        {
            throw new UnauthorizedException("The refresh token is invalid.");
        }

        if (stored.RevokedOn is not null)
        {
            await RevokeAllRefreshTokensAsync(stored.UserId, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("The refresh token is invalid.");
        }

        if (stored.ExpiresOn <= DateTimeOffset.UtcNow)
        {
            stored.RevokedOn = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            throw new UnauthorizedException("The refresh token is invalid.");
        }

        return stored;
    }

    private async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken)
    {
        List<RefreshToken> tokens = await _db.GetRefreshTokensByUserIdAsync(userId, cancellationToken);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (RefreshToken token in tokens)
        {
            token.RevokedOn ??= now;
        }
    }

    private Task<AuthTokensDto> IssueTokensAsync(User user, string? deviceInfo)
    {
        AccessTokenResult access = _jwtTokenService.CreateAccessToken(user);
        string refresh = _jwtTokenService.CreateRefreshTokenValue();
        _db.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = _jwtTokenService.HashRefreshToken(refresh),
            ExpiresOn = DateTimeOffset.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
            CreatedOn = DateTimeOffset.UtcNow,
            DeviceInfo = Truncate(deviceInfo, 256)
        });

        return Task.FromResult(new AuthTokensDto
        {
            AccessToken = access.AccessToken,
            RefreshToken = refresh,
            AccessTokenExpiresOn = access.ExpiresOn
        });
    }

    private static (string Destination, OtpChannel Channel) ResolveDestination(string? mobile, string? email)
    {
        string? normalizedMobile = NormalizeMobile(mobile);
        string? normalizedEmail = NormalizeEmail(email);

        if (normalizedMobile is not null && normalizedEmail is not null)
        {
            throw new ValidationException("Provide a mobile number or an email, not both.");
        }

        if (normalizedMobile is not null)
        {
            return (normalizedMobile, OtpChannel.Sms);
        }

        if (normalizedEmail is not null)
        {
            return (normalizedEmail, OtpChannel.Email);
        }

        throw new ValidationException("A mobile number or email is required.");
    }

    private static string? NormalizeMobile(string? mobile)
    {
        if (string.IsNullOrWhiteSpace(mobile))
        {
            return null;
        }

        return mobile.Trim();
    }

    private static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        string value = email.Trim().ToLowerInvariant();
        if (!value.Contains('@', StringComparison.Ordinal) || value.Length > 256)
        {
            throw new ValidationException("The email address is not valid.");
        }

        return value;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
