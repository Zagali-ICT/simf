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
///
/// <para>H28 — D-088: every method calls
/// <c>cancellationToken.ThrowIfCancellationRequested()</c> at entry, so a
/// pre-cancelled call throws immediately rather than running the
/// underlying <see cref="UserManager{T}"/> operation to completion.
/// `UserManager`'s public API does not accept tokens, so true mid-operation
/// cancellation is the R5-level swap to a Domain-owned user store
/// (the interface keeps the parameter so a future replacement honours
/// it). The entry-throw is the §17 minimum that makes the
/// <see cref="CancellationToken"/> parameter honest at the boundary.</para>
/// </summary>
internal sealed class UserAccountRepository(UserManager<SimfUser> userManager)
    : IUserAccountRepository
{
    public Task<SimfUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.FindByEmailAsync(email);
    }

    public Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.FindByIdAsync(id.ToString());
    }

    public async Task<UserOperationResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.CreateAsync(user, password));
    }

    public async Task<UserOperationResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.CreateAsync(user));
    }

    public async Task<UserOperationResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.UpdateAsync(user));
    }

    public Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.CheckPasswordAsync(user, password);
    }

    public async Task<UserOperationResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.AddPasswordAsync(user, password));
    }

    public async Task<UserOperationResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.RemovePasswordAsync(user));
    }

    public async Task<UserOperationResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.ChangePasswordAsync(user, currentPassword, newPassword));
    }

    public Task UpdateSecurityStampAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.UpdateSecurityStampAsync(user);
    }

    public Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.IsLockedOutAsync(user);
    }

    public Task AccessFailedAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.AccessFailedAsync(user);
    }

    public Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.ResetAccessFailedCountAsync(user);
    }

    public Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.GetRolesAsync(user);
    }

    public Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.IsInRoleAsync(user, role);
    }

    public async Task<UserOperationResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.AddToRoleAsync(user, role));
    }

    public async Task<UserOperationResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.RemoveFromRolesAsync(user, roles));
    }

    public Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.GetAuthenticatorKeyAsync(user);
    }

    public async Task<UserOperationResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.SetAuthenticationTokenAsync(user, loginProvider, tokenName, tokenValue));
    }

    public Task<string?> GetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return userManager.GetAuthenticationTokenAsync(user, loginProvider, tokenName);
    }

    public async Task<UserOperationResult> RemoveAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.RemoveAuthenticationTokenAsync(user, loginProvider, tokenName));
    }

    public async Task<UserOperationResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Translate(await userManager.SetTwoFactorEnabledAsync(user, enabled));
    }

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
