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

    /// <summary>
    /// Returns the signed-in user as the Flutter app consumes it
    /// (<c>GET /api/v1/app/users/me</c>, SIMF-MOB-API-001 §5.1): identity plus
    /// the resolved mobile app-role and the registration status. Available to
    /// any signed-in account — including not-yet-approved ones — so the app can
    /// poll the approval state on the Registration-Status screen (Page 011).
    /// D-249.
    /// </summary>
    Task<CurrentUserResponse> GetCurrentUserAsync(
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

    /// <summary>
    /// Mints a fresh batch of single-use recovery codes for the user,
    /// invalidating any previous batch (D-040). The plaintext codes are
    /// returned exactly once.
    /// </summary>
    Task<RecoveryCodesResponse> RegenerateRecoveryCodesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
