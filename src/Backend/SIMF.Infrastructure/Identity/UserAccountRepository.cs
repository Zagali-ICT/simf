using Microsoft.AspNetCore.Identity;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// <see cref="IUserAccountRepository"/> implementation — a thin pass-through
/// over ASP.NET Core Identity's <see cref="UserManager{T}"/> (R3 — D-076,
/// H21 — D-082). Each method translates the <c>IdentityResult</c> the
/// framework returns into the SIMF-owned <see cref="UserOperationResult"/>,
/// so Application code never sees an Identity type.
/// </summary>
internal sealed class UserAccountRepository(UserManager<SimfUser> userManager)
    : IUserAccountRepository
{
    public Task<SimfUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        userManager.FindByEmailAsync(email);

    public Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        userManager.FindByIdAsync(id.ToString());

    public async Task<UserOperationResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default) =>
        Translate(await userManager.CreateAsync(user, password));

    public async Task<UserOperationResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        Translate(await userManager.CreateAsync(user));

    public async Task<UserOperationResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        Translate(await userManager.UpdateAsync(user));

    public Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default) =>
        userManager.CheckPasswordAsync(user, password);

    public async Task<UserOperationResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default) =>
        Translate(await userManager.AddPasswordAsync(user, password));

    public async Task<UserOperationResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        Translate(await userManager.RemovePasswordAsync(user));

    public async Task<UserOperationResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        Translate(await userManager.ChangePasswordAsync(user, currentPassword, newPassword));

    public Task UpdateSecurityStampAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.UpdateSecurityStampAsync(user);

    public Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.IsLockedOutAsync(user);

    public Task AccessFailedAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.AccessFailedAsync(user);

    public Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.ResetAccessFailedCountAsync(user);

    public Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.GetRolesAsync(user);

    public Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default) =>
        userManager.IsInRoleAsync(user, role);

    public async Task<UserOperationResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default) =>
        Translate(await userManager.AddToRoleAsync(user, role));

    public async Task<UserOperationResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default) =>
        Translate(await userManager.RemoveFromRolesAsync(user, roles));

    public Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.GetAuthenticatorKeyAsync(user);

    public async Task<UserOperationResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default) =>
        Translate(await userManager.SetAuthenticationTokenAsync(user, loginProvider, tokenName, tokenValue));

    public Task<string?> GetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default) =>
        userManager.GetAuthenticationTokenAsync(user, loginProvider, tokenName);

    public async Task<UserOperationResult> RemoveAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default) =>
        Translate(await userManager.RemoveAuthenticationTokenAsync(user, loginProvider, tokenName));

    public async Task<UserOperationResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default) =>
        Translate(await userManager.SetTwoFactorEnabledAsync(user, enabled));

    /// <summary>
    /// H21 — D-082: Identity → SIMF result translation. The error
    /// <c>Code</c> values stay Identity-compatible (e.g. "PasswordMismatch",
    /// "DuplicateEmail") so the existing <c>IdentityErrorTranslator</c>
    /// switch in Application continues to work against the new shape.
    /// </summary>
    private static UserOperationResult Translate(IdentityResult result)
    {
        if (result.Succeeded) return UserOperationResult.Success;
        return UserOperationResult.Failed(
            result.Errors.Select(e => new UserOperationError(e.Code, e.Description)));
    }
}
