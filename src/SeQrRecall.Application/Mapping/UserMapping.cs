using SeQrRecall.Application.Dtos.Users;
using SeQrRecall.Domain.Entities;

namespace SeQrRecall.Application.Mapping;

internal static class UserMapping
{
    public static UserDto ToDto(this User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Mobile = user.Mobile,
            Email = user.Email,
            ProfilePhotoUrl = user.ProfilePhotoUrl,
            IsProfileComplete = !string.IsNullOrWhiteSpace(user.FullName),
            CreatedOn = user.CreatedOn
        };
    }
}
