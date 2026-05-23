using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Admin-driven account-management use cases (<c>myComment</c> #33). First
/// slice — 2FA reset (D-041), create CP user + list users (D-042).
/// </summary>
public interface IAdminAccountService
{
    /// <summary>
    /// Wipes the target user's authenticator key + recovery codes + flips
    /// <c>TwoFactorEnabled</c> off, rolls the security stamp and revokes
    /// every refresh token. Audited with both actor and subject ids and a
    /// mandatory free-text reason (D-041).
    /// </summary>
    Task ResetTwoFactorAsync(
        Guid actorUserId,
        AdminResetTwoFactorRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new Control Panel user account in the <c>Approved</c> state
    /// with no password, mints a 7-day password-set invitation code, and
    /// emails the new user an invitation link. Audited with actor + subject
    /// (D-042).
    /// </summary>
    Task<AdminCreateUserResponse> CreateUserAsync(
        Guid actorUserId,
        AdminCreateUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every account in the system, ordered by creation date (newest
    /// first). The summary carries the email, display name, lifecycle state,
    /// 2FA flag, role flag and creation date. Suitable for an admin table —
    /// not paged today (bounded user count); pagination follows the wider
    /// User Management module (D-042).
    /// </summary>
    Task<AdminUserListResponse> ListUsersAsync(
        CancellationToken cancellationToken = default);
}
