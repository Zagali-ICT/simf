using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Account-management use cases for the signed-in user (myComment item #11):
/// reading the profile, updating the avatar.
/// </summary>
public interface IAccountService
{
    /// <summary>Returns the signed-in user's profile, including a data-URI avatar.</summary>
    Task<ProfileResponse> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Stores a new avatar for the signed-in user.</summary>
    Task<AvatarResponse> SetAvatarAsync(
        Guid userId,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the signed-in user's avatar.</summary>
    Task<AvatarResponse> RemoveAvatarAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
