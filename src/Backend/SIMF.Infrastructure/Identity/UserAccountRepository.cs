using Microsoft.AspNetCore.Identity;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// <see cref="IUserAccountRepository"/> implementation — a thin pass-through
/// over ASP.NET Core Identity's <see cref="UserManager{T}"/> (R3 — D-076).
/// Application services depend on the abstraction; the framework primitive
/// stays in Infrastructure.
/// </summary>
internal sealed class UserAccountRepository(UserManager<SimfUser> userManager)
    : IUserAccountRepository
{
    public Task<SimfUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        userManager.FindByEmailAsync(email);

    public Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        userManager.FindByIdAsync(id.ToString());

    public Task<IdentityResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default) =>
        userManager.CreateAsync(user, password);

    public Task<IdentityResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.CreateAsync(user);

    public Task<IdentityResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.UpdateAsync(user);

    public Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default) =>
        userManager.CheckPasswordAsync(user, password);

    public Task<IdentityResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default) =>
        userManager.AddPasswordAsync(user, password);

    public Task<IdentityResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.RemovePasswordAsync(user);

    public Task<IdentityResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default) =>
        userManager.ChangePasswordAsync(user, currentPassword, newPassword);

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

    public Task<IdentityResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default) =>
        userManager.AddToRoleAsync(user, role);

    public Task<IdentityResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default) =>
        userManager.RemoveFromRolesAsync(user, roles);

    public Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default) =>
        userManager.GetAuthenticatorKeyAsync(user);

    public Task<IdentityResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default) =>
        userManager.SetAuthenticationTokenAsync(user, loginProvider, tokenName, tokenValue);

    public Task<IdentityResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default) =>
        userManager.SetTwoFactorEnabledAsync(user, enabled);
}
