using SIMF.Common.Enums;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Profiles;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// R4 — D-209: persistence seam for <see cref="UserProfileService"/>. The
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

    /// <summary>The bilingual rejection text, or null when none is set.</summary>
    Task<RejectionText?> GetRejectionTextAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The relative path of the stored ID image, or null when the
    /// profile has none.</summary>
    Task<string?> GetIdImagePathAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>V-1 (D-429) — the relative path of the stored VVIP/VIP welcome
    /// photo, or null when the profile has none. A one-column projection (no
    /// tracking) for the per-image read path.</summary>
    Task<string?> GetVipPhotoPathAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The assigned profile type's audience flag + mobile role, or
    /// null when the user has no profile type assigned.</summary>
    Task<ProfileTypeRole?> GetAssignedProfileTypeRoleAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>D-374 — the scalar facts behind the profile-completeness
    /// flag (one projected row, no entity/Interests hydration — this runs on
    /// every <c>/users/me</c> hydration), or null when no profile row exists.</summary>
    Task<ProfileCompletenessFacts?> GetCompletenessFactsAsync(
        Guid userId, CancellationToken cancellationToken = default);

    // --- App DB: lookup validation -----------------------------------------

    /// <summary>The active + scope facts for a profile type, or null when the
    /// id is unknown.</summary>
    Task<ProfileTypeFacts?> FindProfileTypeAsync(
        Guid profileTypeId, CancellationToken cancellationToken = default);

    /// <summary>D-373 — the next value of the registration-reference SQL
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

    /// <summary>B3 — D-221: true when the id is an active <c>Organisation</c>
    /// row. Used to validate the profile's الجهة pick at write time.</summary>
    Task<bool> OrganisationExistsActiveAsync(
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
}

/// <summary>Active + scope facts read off a <see cref="UserProfileType"/>.
/// C5 (D-371) added the audience flag + name so the self-pick lock
/// ("Visitor → Normal only") can be enforced in the service.</summary>
public sealed record ProfileTypeFacts(
    bool IsActive, UserType UserType, bool IsForVisitor, string Name);

/// <summary>Audience flag + mobile role read off an assigned profile type.</summary>
public sealed record ProfileTypeRole(bool IsVisitor, MobileAppRole MobileAppRole);

/// <summary>D-374 — the facts the completeness rule reads (names + ≥1
/// interest + the C7 male-photo rule), projected in one row.</summary>
public sealed record ProfileCompletenessFacts(
    string? Name, string? NameArabic, Gender Gender,
    string? IdImageRelativePath, bool HasInterests);

/// <summary>An approved Admin account — a notification recipient.</summary>
public sealed record PendingAdminRecipient(Guid Id, string? Email, string? DisplayName);
