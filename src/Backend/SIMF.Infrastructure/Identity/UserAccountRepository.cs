using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// <see cref="IUserAccountRepository"/> implementation — a thin pass-through
/// over ASP.NET Core Identity's <see cref="UserManager{T}"/> (R3 — D-076,
/// H21 — D-082). Each method translates the <c>IdentityResult</c> the
/// framework returns into the SIMF-owned <see cref="UserOperationResult"/>,
/// so Application code never sees an Identity type.
///
/// <para>R5a — D-090 / R5b — D-091: <see cref="UserManager{T}"/> is generically
/// parameterised on <see cref="IdentitySimfUser"/> (the Infrastructure-owned
/// persistence shim). The public contract still exposes <see cref="SimfUser"/>;
/// conversion happens at every boundary through
/// <see cref="IdentityUserMapper"/>. R5f drops <c>IdentityUser&lt;Guid&gt;</c>
/// from <see cref="SimfUser"/>'s inheritance chain.</para>
///
/// <para>EF tracking discipline (R5a — D-090). Mutating methods route every
/// operation through <see cref="EnsureTrackedAsync"/>: if the change tracker
/// already holds an <see cref="IdentitySimfUser"/> with the caller's id
/// (always true after a prior <see cref="FindByEmailAsync"/> /
/// <see cref="FindByIdAsync"/> in the same scope), we MERGE the caller's
/// mutations into that tracked instance and pass it to
/// <see cref="UserManager{T}"/>. Otherwise we fetch a fresh tracked instance
/// via <c>UserManager.FindByIdAsync</c>, merge into it, and continue. This
/// keeps EF's concurrency snapshot intact (so the
/// <c>WHERE ConcurrencyStamp = @old</c> on Update matches) and prevents the
/// "another instance with the same key is already tracked" guard from
/// firing when the caller did a find-then-update on the same logical user.
/// Read-only methods (<see cref="CheckPasswordAsync"/>,
/// <see cref="IsLockedOutAsync"/>, <see cref="GetRolesAsync"/>,
/// <see cref="IsInRoleAsync"/>, <see cref="GetAuthenticatorKeyAsync"/>,
/// <see cref="GetAuthenticationTokenAsync"/>) use the same path so role and
/// token store lookups also reuse the tracked instance.</para>
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
internal sealed class UserAccountRepository(
    UserManager<IdentitySimfUser> userManager,
    SimfIdentityDbContext dbContext)
    : IUserAccountRepository
{
    public async Task<SimfUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await userManager.FindByEmailAsync(email);
        return identity is null ? null : IdentityUserMapper.ToDomain(identity);
    }

    public async Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await userManager.FindByIdAsync(id.ToString());
        return identity is null ? null : IdentityUserMapper.ToDomain(identity);
    }

    public async Task<UserOperationResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Create has no existing row to track-merge against — pass a fresh
        // IdentitySimfUser and sync back the server-generated fields.
        var identity = IdentityUserMapper.ToIdentity(user);
        var result = await userManager.CreateAsync(identity, password);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = IdentityUserMapper.ToIdentity(user);
        var result = await userManager.CreateAsync(identity);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.UpdateAsync(identity);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // CheckPasswordAsync re-attaches and re-stamps on the SuccessRehashNeeded
        // branch, so a tracked entity is required to avoid the duplicate-tracking
        // guard. `merge: false` keeps stale caller fields from clobbering the
        // tracked row before the rehash UPDATE.
        var identity = await EnsureTrackedAsync(user, merge: false) ?? IdentityUserMapper.ToIdentity(user);
        return await userManager.CheckPasswordAsync(identity, password);
    }

    public async Task<UserOperationResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.AddPasswordAsync(identity, password);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.RemovePasswordAsync(identity);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.ChangePasswordAsync(identity, currentPassword, newPassword);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task UpdateSecurityStampAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return; }
        var result = await userManager.UpdateSecurityStampAsync(identity);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
    }

    public async Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? IdentityUserMapper.ToIdentity(user);
        return await userManager.IsLockedOutAsync(identity);
    }

    public async Task AccessFailedAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return; }
        var result = await userManager.AccessFailedAsync(identity);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
    }

    public async Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return; }
        var result = await userManager.ResetAccessFailedCountAsync(identity);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
    }

    public async Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? IdentityUserMapper.ToIdentity(user);
        return await userManager.GetRolesAsync(identity);
    }

    public async Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? IdentityUserMapper.ToIdentity(user);
        return await userManager.IsInRoleAsync(identity, role);
    }

    public async Task<UserOperationResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.AddToRoleAsync(identity, role);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.RemoveFromRolesAsync(identity, roles);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? IdentityUserMapper.ToIdentity(user);
        return await userManager.GetAuthenticatorKeyAsync(identity);
    }

    public async Task<UserOperationResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.SetAuthenticationTokenAsync(identity, loginProvider, tokenName, tokenValue);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<string?> GetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? IdentityUserMapper.ToIdentity(user);
        return await userManager.GetAuthenticationTokenAsync(identity, loginProvider, tokenName);
    }

    public async Task<UserOperationResult> RemoveAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.RemoveAuthenticationTokenAsync(identity, loginProvider, tokenName);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.SetTwoFactorEnabledAsync(identity, enabled);
        if (result.Succeeded) { IdentityUserMapper.SyncBack(identity, user); }
        return Translate(result);
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

    private static UserOperationResult MissingUser() =>
        UserOperationResult.Failed("UserNotFound", "The user does not exist.");

    /// <summary>
    /// R5a — D-090: returns the EF-tracked <see cref="IdentitySimfUser"/> for
    /// the supplied <see cref="SimfUser"/>. Reuses the already-tracked
    /// instance if one exists in the scope (the common case — caller did a
    /// prior <c>FindBy</c>); otherwise fetches a fresh one via
    /// <c>UserManager.FindByIdAsync</c> so the change-tracker snapshot
    /// carries the correct ORIGINAL ConcurrencyStamp. Returns <c>null</c>
    /// when the user does not exist in the DB.
    ///
    /// <para>When <paramref name="merge"/> is true (the default — used by
    /// mutating wrappers), the caller's in-memory mutations are copied onto
    /// the tracked entity via <see cref="ApplyDomainMutations"/> so the
    /// subsequent <see cref="UserManager{T}"/> call sees what the caller
    /// intended. Read-only wrappers pass <c>merge: false</c> so a stale
    /// caller-side <see cref="SimfUser"/> cannot clobber server-mutated
    /// columns (lockout counters, etc.) on the tracked row.</para>
    /// </summary>
    private async Task<IdentitySimfUser?> EnsureTrackedAsync(SimfUser user, bool merge = true)
    {
        var tracked = dbContext.Set<IdentitySimfUser>().Local.FindEntry(user.Id)?.Entity
            ?? await userManager.FindByIdAsync(user.Id.ToString());
        if (tracked is null) { return null; }

        if (merge) { IdentityUserMapper.ApplyDomainMutations(user, tracked); }
        return tracked;
    }
}
