using SIMF.Common;
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
    /// Returns one page of accounts (decision D-044). Sortable on Email /
    /// DisplayName / AccountState / CreatedAt; filterable on the same set
    /// plus a free-text Search across email and display name. The endpoint
    /// clamps <c>Top</c> to a sensible ceiling.
    /// </summary>
    Task<GridPage<AdminUserSummary>> ListUsersAsync(
        GridQuery query,
        CancellationToken cancellationToken = default);
}
