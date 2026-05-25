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
/// <para>R5a — D-090: <see cref="UserManager{T}"/> is now generically
/// parameterised on <see cref="IdentitySimfUser"/> (the Infrastructure-owned
/// persistence shim). The public contract still exposes <see cref="SimfUser"/>;
/// conversion happens at every boundary through the inline proto-mapper
/// (<see cref="ToIdentity"/> / <see cref="ToDomain"/> / <see cref="SyncBack"/>).
/// R5b extracts this mapper into a dedicated <c>IdentityUserMapper</c> class;
/// R5f drops <c>IdentityUser&lt;Guid&gt;</c> from <see cref="SimfUser"/>'s
/// inheritance chain.</para>
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
        return identity is null ? null : ToDomain(identity);
    }

    public async Task<SimfUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await userManager.FindByIdAsync(id.ToString());
        return identity is null ? null : ToDomain(identity);
    }

    public async Task<UserOperationResult> CreateAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Create has no existing row to track-merge against — pass a fresh
        // IdentitySimfUser and sync back the server-generated fields.
        var identity = ToIdentity(user);
        var result = await userManager.CreateAsync(identity, password);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> CreateAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = ToIdentity(user);
        var result = await userManager.CreateAsync(identity);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> UpdateAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.UpdateAsync(identity);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // CheckPasswordAsync re-attaches and re-stamps on the SuccessRehashNeeded
        // branch, so a tracked entity is required to avoid the duplicate-tracking
        // guard. `merge: false` keeps stale caller fields from clobbering the
        // tracked row before the rehash UPDATE.
        var identity = await EnsureTrackedAsync(user, merge: false) ?? ToIdentity(user);
        return await userManager.CheckPasswordAsync(identity, password);
    }

    public async Task<UserOperationResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.AddPasswordAsync(identity, password);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.RemovePasswordAsync(identity);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.ChangePasswordAsync(identity, currentPassword, newPassword);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task UpdateSecurityStampAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return; }
        var result = await userManager.UpdateSecurityStampAsync(identity);
        if (result.Succeeded) { SyncBack(identity, user); }
    }

    public async Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? ToIdentity(user);
        return await userManager.IsLockedOutAsync(identity);
    }

    public async Task AccessFailedAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return; }
        var result = await userManager.AccessFailedAsync(identity);
        if (result.Succeeded) { SyncBack(identity, user); }
    }

    public async Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return; }
        var result = await userManager.ResetAccessFailedCountAsync(identity);
        if (result.Succeeded) { SyncBack(identity, user); }
    }

    public async Task<IList<string>> GetRolesAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? ToIdentity(user);
        return await userManager.GetRolesAsync(identity);
    }

    public async Task<bool> IsInRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? ToIdentity(user);
        return await userManager.IsInRoleAsync(identity, role);
    }

    public async Task<UserOperationResult> AddToRoleAsync(SimfUser user, string role, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.AddToRoleAsync(identity, role);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> RemoveFromRolesAsync(SimfUser user, IEnumerable<string> roles, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.RemoveFromRolesAsync(identity, roles);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? ToIdentity(user);
        return await userManager.GetAuthenticatorKeyAsync(identity);
    }

    public async Task<UserOperationResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.SetAuthenticationTokenAsync(identity, loginProvider, tokenName, tokenValue);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<string?> GetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user, merge: false) ?? ToIdentity(user);
        return await userManager.GetAuthenticationTokenAsync(identity, loginProvider, tokenName);
    }

    public async Task<UserOperationResult> RemoveAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.RemoveAuthenticationTokenAsync(identity, loginProvider, tokenName);
        if (result.Succeeded) { SyncBack(identity, user); }
        return Translate(result);
    }

    public async Task<UserOperationResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identity = await EnsureTrackedAsync(user);
        if (identity is null) { return MissingUser(); }
        var result = await userManager.SetTwoFactorEnabledAsync(identity, enabled);
        if (result.Succeeded) { SyncBack(identity, user); }
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

        if (merge) { ApplyDomainMutations(user, tracked); }
        return tracked;
    }

    /// <summary>R5a — D-090: SimfUser → IdentitySimfUser (going INTO Identity).</summary>
    private static IdentitySimfUser ToIdentity(SimfUser source) => new()
    {
        Id = source.Id,
        UserName = source.UserName,
        NormalizedUserName = source.NormalizedUserName,
        Email = source.Email,
        NormalizedEmail = source.NormalizedEmail,
        EmailConfirmed = source.EmailConfirmed,
        PasswordHash = source.PasswordHash,
        SecurityStamp = source.SecurityStamp,
        ConcurrencyStamp = source.ConcurrencyStamp,
        PhoneNumber = source.PhoneNumber,
        PhoneNumberConfirmed = source.PhoneNumberConfirmed,
        TwoFactorEnabled = source.TwoFactorEnabled,
        LockoutEnd = source.LockoutEnd,
        LockoutEnabled = source.LockoutEnabled,
        AccessFailedCount = source.AccessFailedCount,
        DisplayName = source.DisplayName,
        AccountState = source.AccountState,
        UserType = source.UserType,
        PasswordChangeRequired = source.PasswordChangeRequired,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        LastUsedTotpTimestep = source.LastUsedTotpTimestep,
        AvatarRelativePath = source.AvatarRelativePath,
        QrId = source.QrId,
        RejectionReason = source.RejectionReason,
        RejectionReasonArabic = source.RejectionReasonArabic,
        StateChangedAt = source.StateChangedAt,
        StateChangedByUserId = source.StateChangedByUserId,
    };

    /// <summary>R5a — D-090: IdentitySimfUser → SimfUser (coming OUT of Identity).</summary>
    private static SimfUser ToDomain(IdentitySimfUser source) => new()
    {
        Id = source.Id,
        UserName = source.UserName,
        NormalizedUserName = source.NormalizedUserName,
        Email = source.Email,
        NormalizedEmail = source.NormalizedEmail,
        EmailConfirmed = source.EmailConfirmed,
        PasswordHash = source.PasswordHash,
        SecurityStamp = source.SecurityStamp,
        ConcurrencyStamp = source.ConcurrencyStamp,
        PhoneNumber = source.PhoneNumber,
        PhoneNumberConfirmed = source.PhoneNumberConfirmed,
        TwoFactorEnabled = source.TwoFactorEnabled,
        LockoutEnd = source.LockoutEnd,
        LockoutEnabled = source.LockoutEnabled,
        AccessFailedCount = source.AccessFailedCount,
        DisplayName = source.DisplayName,
        AccountState = source.AccountState,
        UserType = source.UserType,
        PasswordChangeRequired = source.PasswordChangeRequired,
        CreatedAt = source.CreatedAt,
        UpdatedAt = source.UpdatedAt,
        LastUsedTotpTimestep = source.LastUsedTotpTimestep,
        AvatarRelativePath = source.AvatarRelativePath,
        QrId = source.QrId,
        RejectionReason = source.RejectionReason,
        RejectionReasonArabic = source.RejectionReasonArabic,
        StateChangedAt = source.StateChangedAt,
        StateChangedByUserId = source.StateChangedByUserId,
    };

    /// <summary>
    /// R5a — D-090: copies the caller's in-memory mutations from the SimfUser
    /// onto the EF-tracked IdentitySimfUser. The IdentityUser base fields
    /// (ConcurrencyStamp, SecurityStamp, NormalizedXxx, PasswordHash etc.)
    /// are NOT copied — UserManager owns those and mutates the tracked
    /// instance directly. The custom SIMF columns (DisplayName,
    /// AccountState, EmailConfirmed, the lockout pair, the 2FA flag, and
    /// the per-row lifecycle metadata) ARE copied so the next
    /// <c>UserManager.UpdateAsync</c> persists what the caller intended.
    /// </summary>
    private static void ApplyDomainMutations(SimfUser source, IdentitySimfUser target)
    {
        target.UserName = source.UserName;
        target.Email = source.Email;
        target.EmailConfirmed = source.EmailConfirmed;
        target.PhoneNumber = source.PhoneNumber;
        target.PhoneNumberConfirmed = source.PhoneNumberConfirmed;
        target.TwoFactorEnabled = source.TwoFactorEnabled;
        target.LockoutEnd = source.LockoutEnd;
        target.LockoutEnabled = source.LockoutEnabled;
        target.AccessFailedCount = source.AccessFailedCount;
        target.DisplayName = source.DisplayName;
        target.AccountState = source.AccountState;
        target.UserType = source.UserType;
        target.PasswordChangeRequired = source.PasswordChangeRequired;
        target.CreatedAt = source.CreatedAt;
        target.UpdatedAt = source.UpdatedAt;
        target.LastUsedTotpTimestep = source.LastUsedTotpTimestep;
        target.AvatarRelativePath = source.AvatarRelativePath;
        target.QrId = source.QrId;
        target.RejectionReason = source.RejectionReason;
        target.RejectionReasonArabic = source.RejectionReasonArabic;
        target.StateChangedAt = source.StateChangedAt;
        target.StateChangedByUserId = source.StateChangedByUserId;
    }

    /// <summary>
    /// R5a — D-090: copies server-side mutations (security stamp, lockout
    /// state, password hash, access-failed counter, etc.) back from the
    /// IdentitySimfUser the UserManager touched into the caller's SimfUser
    /// instance. Preserves the pre-R5a contract where mutating methods
    /// visibly mutate the passed-in user object.
    /// </summary>
    private static void SyncBack(IdentitySimfUser source, SimfUser target)
    {
        target.Id = source.Id;
        target.UserName = source.UserName;
        target.NormalizedUserName = source.NormalizedUserName;
        target.NormalizedEmail = source.NormalizedEmail;
        target.EmailConfirmed = source.EmailConfirmed;
        target.PasswordHash = source.PasswordHash;
        target.SecurityStamp = source.SecurityStamp;
        target.ConcurrencyStamp = source.ConcurrencyStamp;
        target.PhoneNumber = source.PhoneNumber;
        target.PhoneNumberConfirmed = source.PhoneNumberConfirmed;
        target.TwoFactorEnabled = source.TwoFactorEnabled;
        target.LockoutEnd = source.LockoutEnd;
        target.LockoutEnabled = source.LockoutEnabled;
        target.AccessFailedCount = source.AccessFailedCount;
    }
}
