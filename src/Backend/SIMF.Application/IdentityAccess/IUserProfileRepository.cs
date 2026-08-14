using SIMF.Common.Enums;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Persistence seam for <see cref="UserProfileService"/>. The
/// service genuinely spans both databases — the profile + its lookups
/// (ProfileType, Interest, Country) live on the App DB, while the owning
/// account + admin recipients live on the Identity DB — so this gateway
/// exposes both, with the save split (<see cref="SaveIdentityChangesAsync"/>
/// runs inside the <c>ITransactionRunner</c> transaction; the App save runs
/// after it). Query shapes are lifted verbatim from the pre-move service.
/// </summary>
public interface IUserProfileRepository
{
    // --- App DB: the profile row -------------------------------------------

    /// <summary>The profile with its Interests loaded, or null. Pass
    /// <paramref name="tracked"/> = true for the upsert path (the row is
    /// mutated), false for the read path.</summary>
    Task<UserProfile?> GetWithInterestsAsync(
        Guid userId, bool tracked, CancellationToken cancellationToken = default);

    /// <summary>The tracked profile row (no Interests include), or null —
    /// used by the ID-image upsert paths.</summary>
    Task<UserProfile?> FindAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Stages a new profile row (saved by a later
    /// <see cref="SaveAppChangesAsync"/>).</summary>
    void Add(UserProfile profile);

    /// <summary>True when ANOTHER profile (<c>UserId != excludeUserId</c>)
    /// already carries one of the supplied non-null identity blind-index hashes
    /// (National ID / Iqama / passport). The self-exclusion lets a user re-save
    /// their OWN id without a false duplicate. Null hashes are ignored. A plain
    /// single-context read on the App DB — never a cross-DB JOIN.</summary>
    Task<bool> AnyOtherProfileWithIdentityHashAsync(
        Guid excludeUserId, string? nationalIdHash, string? iqamaNumberHash,
        string? passportNumberHash, CancellationToken cancellationToken = default);

    /// <summary>The bilingual rejection text, or null when none is set.</summary>
    Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The active <c>StoredFile</c>'s storage locator
    /// (key + content-type + encrypted flag) for a Confidential owner-scoped service
    /// (<see cref="FileService.VipPhoto"/>, <see cref="FileService.IdDocument"/>) + an
    /// owner, or null when none. Owner-scoped (<c>Service=service,
    /// OwnerEntityId=ownerUserId</c>) in the same App DB — no cross-context read.</summary>
    Task<(string StorageKey, string? ContentType, bool IsEncrypted)?> GetOwnerScopedFileAsync(
        FileService service, Guid ownerUserId, CancellationToken cancellationToken = default);

    /// <summary>The assigned profile type's audience flag + mobile role, or
    /// null when the user has no profile type assigned.</summary>
    Task<ProfileTypeRole?> GetAssignedProfileTypeRoleAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The scalar facts behind the profile-completeness
    /// flag (one projected row, no entity/Interests hydration — this runs on
    /// every <c>/users/me</c> hydration), or null when no profile row exists.</summary>
    Task<ProfileCompletenessFacts?> GetCompletenessFactsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    // --- App DB: lookup validation -----------------------------------------

    /// <summary>The active + scope facts for a profile type, or null when the
    /// id is unknown.</summary>
    Task<ProfileTypeFacts?> FindProfileTypeAsync(
        Guid profileTypeId, CancellationToken cancellationToken = default);

    /// <summary>The next value of the registration-reference SQL
    /// sequence (concurrency-safe, monotonic). The service formats it as
    /// <c>SIMF-&lt;year&gt;-&lt;value:D8&gt;</c>.</summary>
    Task<long> NextRegistrationReferenceAsync(
        CancellationToken cancellationToken = default);

    /// <summary>The subset of <paramref name="ids"/> that are active
    /// interests — the count is compared against the request to reject
    /// unknown / deactivated picks.</summary>
    Task<IReadOnlyList<Guid>> FilterActiveInterestIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>The tracked Interest rows for <paramref name="ids"/> — added
    /// to the profile's Interests collection on the diff path.</summary>
    Task<IReadOnlyList<UserInterest>> GetInterestsByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

    /// <summary>Country PK for an active ISO code, or null when unknown.</summary>
    Task<int?> ResolveCountryIdAsync(
        string code, CancellationToken cancellationToken = default);

    /// <summary>ISO code for a Country PK, or empty when unknown / zero.</summary>
    Task<string> ResolveCountryCodeAsync(
        int id, CancellationToken cancellationToken = default);

    /// <summary>True when the id is an active <c>Organisation</c>
    /// row. Used to validate the profile's الجهة pick at write time.</summary>
    Task<bool> OrganisationExistsActiveAsync(
        Guid id, CancellationToken cancellationToken = default);

    /// <summary>True when the id is an active <c>Region</c>
    /// row. Used to validate the profile's المنطقة pick at write time.</summary>
    Task<bool> RegionExistsActiveAsync(
        Guid id, CancellationToken cancellationToken = default);

    // --- Identity DB: account reads ----------------------------------------

    /// <summary>Every approved Admin — the recipients of the new-pending-
    /// visitor notification.</summary>
    Task<IReadOnlyList<PendingAdminRecipient>> ListApprovedAdminsAsync(
        CancellationToken cancellationToken = default);

    // --- Saves (two-phase commit) ------------------------------------------

    /// <summary>Persists pending Identity-context changes. Called inside the
    /// <c>ITransactionRunner</c> transaction so it commits atomically with the
    /// account-state flip + refresh-token revoke.</summary>
    Task SaveIdentityChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists pending App-context changes (profile row +
    /// interests). Called only after the Identity transaction succeeds, so an
    /// Identity-side failure leaves the profile unsaved.</summary>
    Task SaveAppChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>FIX E — like <see cref="SaveAppChangesAsync"/> for the self-service
    /// profile upsert, but translates the filtered UNIQUE identity-index race (a
    /// concurrent duplicate National ID / Iqama / passport that slipped the soft
    /// duplicate-identity guard) into a 409 <c>DuplicateIdentity</c>. Any other
    /// <c>DbUpdateException</c> is rethrown unchanged. Lives here because the
    /// Application layer does not reference EF Core / <c>DbUpdateException</c>.</summary>
    Task SaveProfileIdentityChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Active + scope facts read off a <see cref="UserProfileType"/>.
/// The audience flag + name are carried so the self-pick lock
/// ("Visitor → Normal only") can be enforced in the service.</summary>
public sealed record ProfileTypeFacts(
    bool IsActive, UserType UserType, bool IsForVisitor, string Name,
    // The VIP-tier flag (VVIP/VIP), so the app profile
    // read can report IsVip for the speaker-meeting CTA gate.
    bool IsVipTier,
    // The self-registration picker visibility flag, so the
    // self-service write path (UpsertMineAsync) can reject a self-picked CP-only
    // (IsAppRegisterable=false) operational type, mirroring the server-side filter
    // on GET /app/account/profile-types instead of trusting the client.
    bool IsAppRegisterable);

/// <summary>Audience flag + mobile role read off an assigned profile type.</summary>
public sealed record ProfileTypeRole(bool IsVisitor, MobileAppRole MobileAppRole);

/// <summary>The facts the completeness rule reads (names + ≥1
/// interest + the male-photo rule), projected in one row.
/// <para><paramref name="IsVisitorProfileType"/> says whether the
/// row belongs to an AUDIENCE registrant (no profile type yet, or one with
/// <c>IsForVisitor=true</c>). The interest / ID-document / male-face evidence rules
/// are a visitor-registration requirement and must not lock an operational
/// partner-side account (a gate operator, a moderator) out of the app. Appended
/// with a default so the record stays append-only.</para></summary>
public sealed record ProfileCompletenessFacts(
    string? Name, string? NameArabic, Gender Gender,
    Guid? IdImageFileId, bool HasInterests,
    bool IsVisitorProfileType = true);

/// <summary>An approved Admin account — a notification recipient.</summary>
public sealed record PendingAdminRecipient(Guid Id, string? Email, string? DisplayName);
