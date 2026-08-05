using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Organisations;

namespace SIMF.Domain.Profiles;

/// <summary>
/// The per-account information captured at registration. One row per non-Admin
/// <see cref="IdentityAccess.SimfUser"/>, absent until the user fills the form in or an admin
/// creates a stub on their behalf; Admin-typed users carry no profile.
///
/// <para><see cref="ProfileTypeId"/> hangs off the profile rather than off the
/// user because the subtype is a property of the profile, not of the identity:
/// an audience tier (VVIP, VIP, Gold) or a partner kind (Staff, Exhibitor,
/// Sponsor), told apart by <see cref="UserProfileType.IsForVisitor"/>.</para>
/// </summary>
public class UserProfile : BaseAuditEntity
{
    /// <summary>The owning account. A bare Guid rather than a navigation: the
    /// user row lives in the Identity database, so there is no foreign key
    /// across the two and the service layer enforces the link on write. Unique,
    /// so a second upsert by the same user updates this row instead of adding a
    /// sibling.</summary>
    public Guid UserId { get; set; }

    /// <summary>Full name in English, exactly as printed in the passport.</summary>
    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    /// <summary><see cref="Gender.Unspecified"/> until the user picks.</summary>
    public Gender Gender { get; set; }

    public DateOnly? DateOfBirth { get; set; }

    public string PlaceOfBirth { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 numeric country id. Deliberately NOT a database
    /// foreign key even though Country sits in the same context, because 0 means
    /// "no nationality chosen" on the profile stubs an admin-create, a walk-in
    /// pre-fill or a seeded fixture leaves behind, and a real key would forbid
    /// that. Validated in the service layer instead.</summary>
    public int NationalityId { get; set; }

    /// <summary>Chooses which of the three identity documents below is
    /// required.</summary>
    public bool IsSaudi { get; set; }

    // A Saudi carries a national id (10 digits), a non-Saudi resident an iqama
    // (10 digits), and anyone else a passport number. All three are encrypted at
    // rest under a random nonce, which is why none of them can be equality-queried
    // or unique-indexed, and why the blind-index columns below exist.
    public string? NationalId { get; set; }

    public string? IqamaNumber { get; set; }

    public string? PassportNumber { get; set; }

    // Deterministic blind indexes of the normalised document numbers above
    // (keyed HMAC-SHA256, 64 hex chars). The plaintext columns cannot be
    // unique-indexed, so the "no two profiles share an identity document" rule is
    // enforced by a filtered UNIQUE index on these stable digests instead.
    // Computed on write by the walk-in registration path; null when the matching
    // number is unset, or when the row was written by a path that does not
    // compute them. Deliberately outside the SimfAppDbContext PII-encryption
    // converter loop: encrypting a digest would destroy the determinism the
    // index depends on.
    public string? NationalIdHash { get; set; }

    public string? IqamaNumberHash { get; set; }

    public string? PassportNumberHash { get; set; }

    /// <summary>Optional professional title shown beside the name, e.g.
    /// "Captain" or "Director of Operations".</summary>
    public string? JobTitle { get; set; }

    /// <summary>Arabic twin of <see cref="JobTitle"/>, so a bilingual surface
    /// renders the title in the active locale. Where both are unset, a
    /// head-of-delegation title falls back to <see cref="Honorific"/>.</summary>
    public string? JobTitleArabic { get; set; }

    /// <summary>
    /// The visitor's organisation or employer, picked from the curated
    /// <see cref="Organisation"/> lookup that the team bulk-loads from a
    /// government spreadsheet. Unlike <see cref="NationalityId"/> this IS a real
    /// database foreign key (<c>OnDelete.Restrict</c>), which is safe because the
    /// column is nullable, so stubs simply leave it null.
    /// </summary>
    public Guid? OrganisationId { get; set; }

    public Organisation? Organisation { get; set; }

    /// <summary>The visitor's region, picked from the curated
    /// <see cref="SIMF.Domain.Regions.Region"/> lookup. Optional, and a real
    /// foreign key of the same shape as <see cref="OrganisationId"/>.</summary>
    public Guid? RegionId { get; set; }

    public SIMF.Domain.Regions.Region? Region { get; set; }

    /// <summary>The bulk-badge run that minted this placeholder profile, so a
    /// generated set can be re-emailed or revoked together. Null for every
    /// profile created through the normal sign-up or walk-in paths. A real
    /// foreign key of the same shape as <see cref="OrganisationId"/>.</summary>
    public Guid? BadgeBatchId { get; set; }

    public SIMF.Domain.Badges.BadgeBatch? BadgeBatch { get; set; }

    /// <summary>Whether this profile appears in the "Meet People Like You"
    /// recommendations and the partner directory. The column defaults to true, so
    /// a row stays visible until the user opts out, and the admin-side master
    /// switch <see cref="UserProfileType.ShowInPartnerDirectory"/> is ANDed with
    /// it.</summary>
    public bool ShowInMeetLikeYou { get; set; }

    // Both optional: a Saudi mobile is +966xxxxxxxxx, an international one is
    // +cc-local.
    public string? SaudiMobile { get; set; }

    public string? InternationalMobile { get; set; }

    /// <summary>Optional Saudi vehicle plate, stored normalised: 3 letters then
    /// 1 to 4 digits, no separators, 7 characters at most.</summary>
    public string? PlateNumber { get; set; }

    /// <summary>The human-friendly registration reference
    /// (<c>SIMF-2026-00000001</c>), issued once from the
    /// <c>RegistrationReferenceSequence</c> SQL sequence when the row is first
    /// created and unique thereafter. It is the customer-facing lookup reference
    /// and <b>not</b> the QR id.</summary>
    public string? ReferenceNumber { get; set; }

    // VVIP/VIP extras for the موج (Mawj) welcome-message integration. All
    // optional, and only populated for VVIP/VIP visitors.

    public string? MawjId { get; set; }

    /// <summary>The honorific, e.g. "Minister".</summary>
    public string? Honorific { get; set; }

    /// <summary>Arabic twin of <see cref="Honorific"/>.</summary>
    public string? HonorificArabic { get; set; }

    /// <summary>An IETF language tag ("ar" or "en"), the language the VIP welcome
    /// message goes out in.</summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>Relative path of a separate high-resolution VIP photo, distinct
    /// from the account avatar and held in the VIP-photo store.</summary>
    public string? VipPhotoRelativePath { get; set; }

    /// <summary>Marks the profile as a delegation member (وفد); a delegate is
    /// otherwise an ordinary visitor. The create path refuses the flag unless the
    /// nationality is an invited country (<see cref="Common.Country.IsInvited"/>).
    /// It is what puts the person on their country's public delegation listing,
    /// and it no longer decides who may request a delegation meeting.</summary>
    public bool IsDelegate { get; set; }

    /// <summary>Admin-assigned eligibility to request a <b>delegation
    /// (country-to-country) meeting</b>. It replaced <see cref="IsDelegate"/> as
    /// the requester gate, so a user may now qualify whatever their user or
    /// profile type; the target country must still be an invited delegation
    /// (<see cref="Common.Country.IsInvited"/>).</summary>
    public bool AllowsDelegationMeeting { get; set; }

    /// <summary>Admin-assigned eligibility to request a <b>speaker meeting</b>.
    /// It replaced the VIP-tier test
    /// (<see cref="UserProfileType.AllowsVipMeetingSlots"/>) as the requester
    /// gate, so eligibility no longer follows the tier; the speaker must still
    /// opt in through <c>Speaker.AllowsMeetingRequests</c>.</summary>
    public bool AllowsSpeakerMeeting { get; set; }

    /// <summary>
    /// The assigned <see cref="ProfileType"/>, null until an admin assigns one.
    /// <c>AdminAccountService</c> checks the chosen row is active and sits on the
    /// side of <see cref="UserProfileType.IsForVisitor"/> that matches the queue
    /// the account is created or approved through, so a partner type can never be
    /// pinned on an audience account.
    /// </summary>

    public Guid? ProfileTypeId { get; set; }

    public UserProfileType? ProfileType { get; set; }
    /// <summary>
    /// The interests the user picked, many-to-many through the auto-generated
    /// <c>UserProfileInterests</c> join table. The validator requires between 1
    /// and 10 on every upsert, and the service rejects unknown or deactivated ids.
    /// </summary>
    public ICollection<UserInterest> Interests { get; set; } = new List<UserInterest>();

    /// <summary>
    /// The short, opaque event-entry identifier carried in the participant's QR
    /// code, which staff scan to check them in. Minted by <c>IQrIdMinter</c> the
    /// moment the owning account's <see cref="IdentityAccess.SimfUser.AccountState"/> reaches
    /// <see cref="AccountState.Approved"/>, so it stays null until the account is
    /// approved. Twelve characters of the Crockford base32 alphabet (no 0, O, 1,
    /// I, L or U), unique across the system.
    /// </summary>
    public string? QrId { get; set; }

    /// <summary>
    /// The admin's reason for rejecting the account, written when the owning
    /// account's <see cref="IdentityAccess.SimfUser.AccountState"/> reaches
    /// <see cref="AccountState.Rejected"/> and cleared on a later approval. Up to
    /// 500 characters, matching the <c>AdminRejectRequest.Reason</c> validator.
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// The Arabic version. The admin form takes only the English reason today, so
    /// the service mirrors it here as a graceful fallback; a bilingual form could
    /// later let the two diverge.
    /// </summary>
    public string? RejectionReasonArabic { get; set; }

    /// <summary>Relative path of the ID-image file inside the unified store
    /// rooted at <c>FileStorage:RootPath</c>, under its <c>IdDocument</c> folder.
    /// The bytes are envelope-encrypted on disk and never sit in the row. Null
    /// when no image has been uploaded.</summary>
    public string? IdImageRelativePath { get; set; }

    // The five accessibility choices the app used to keep in device preferences
    // ONLY, so they never followed the user to a second device and did not
    // survive a reinstall. They are per-account settings, so they live on the
    // profile row (which already carries the bare UserId). Served by
    // GET / PUT /app/account/preferences.
    // Tests: SIMF.Api.Tests/AccountPreferencesTests.cs

    /// <summary>The value a profile row carries until the user picks another.
    /// Must stay equal to
    /// <c>SIMF.Contracts.Account.AccountPreferences.DefaultTextSize</c>; the
    /// Domain project does not reference Contracts, so the two constants are
    /// pinned together by a test rather than by the compiler.</summary>
    public const string DefaultAccessibilityTextSize = "normal";

    /// <summary>Stored as the app's <b>stable enum name</b> (<c>small</c>,
    /// <c>normal</c>, <c>large</c>, <c>extraLarge</c>) and NOT as an index, so
    /// reordering the client enum can never re-interpret stored rows.
    /// Case-sensitive: the app matches the name byte for byte.</summary>
    public string AccessibilityTextSize { get; set; } = DefaultAccessibilityTextSize;

    public bool AccessibilityHighContrast { get; set; }

    /// <summary>Suppresses non-essential animation.</summary>
    public bool AccessibilityReduceMotion { get; set; }

    /// <summary>Announces each screen through the platform accessibility
    /// channel.</summary>
    public bool AccessibilityScreenReaderAssist { get; set; }

    /// <summary>The live and session caption strip.</summary>
    public bool AccessibilityCaptions { get; set; } = true;

    /// <summary>When the account last SAVED its accessibility choices, or null if
    /// it never has. Saudi wall clock, like every timestamp here.
    ///
    /// <para>This exists because the five columns above cannot distinguish "the
    /// user chose the defaults" from "nobody has chosen anything" — they hold the
    /// default either way. The app applies whatever the GET returns over its local
    /// cache at sign-in, so without this marker a low-vision user who had already
    /// set extraLarge + high contrast on the device would be silently RESET to
    /// normal the first time they signed in after the endpoint shipped. A null
    /// here answers the GET with <c>configured: false</c>, and the app then keeps
    /// its local choices and uploads them instead of overwriting them.</para></summary>
    public DateTime? AccessibilityConfiguredAt { get; set; }
}
