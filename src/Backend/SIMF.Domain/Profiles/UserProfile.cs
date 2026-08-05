using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Organisations;

namespace SIMF.Domain.Profiles;

/// <summary>
/// A user's profile — the per-account information captured at registration
/// (decisions D-046, D-048). One row per non-Admin <see cref="SimfUser"/>;
/// null until the user fills the form or an admin creates a stub on the
/// user's behalf. Admin-typed users do not carry a profile today.
///
/// <para>The <see cref="ProfileTypeId"/> FK lives here (P8 — D-049) rather
/// than on <see cref="SimfUser"/>, because the profile-type is a property
/// of the profile, not of the user identity. Visitors get a tier (VVIP /
/// VIP / Gold / …); Others get a partner kind (Staff / Exhibitor /
/// Sponsor / …); the discriminator on <see cref="UserProfileType.UserType"/>
/// keeps each lookup row scoped to a single user kind.</para>
///
/// <para>The ID-image attachment is stored on disk encrypted-at-rest via
/// <c>IUserIdDocumentStorage</c>. The relative path lives here; the bytes
/// never sit in the row.</para>
/// </summary>
public class UserProfile : BaseAuditEntity
{
    /// <summary>The owning user (logical FK to <c>SimfUser.Id</c> on the
    /// Identity DB — D-167 moved this entity to the App DB so the FK is
    /// enforced at write time by the service layer, not by a DB
    /// constraint).</summary>
    public Guid UserId { get; set; }

    /// <summary>Full name in English exactly as printed in the passport.</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Full name in Arabic.</summary>
    public string NameArabic { get; set; } = string.Empty;

  
    /// <summary>B3 — D-221 (الجنس): the user's gender, captured on the profile /
    /// sign-up screen (Mockup screen 05). <see cref="Gender.Unspecified"/> until
    /// the user picks.</summary>
    public Gender Gender { get; set; }

    /// <summary>Date of birth (date only).</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Place of birth — free text, up to 128 chars.</summary>
    public string PlaceOfBirth { get; set; } = string.Empty;

    /// <summary>D-151 — ISO 3166-1 numeric country id; real FK to
    /// <c>SIMF.Domain.Common.Country.Id</c> after D-167 moved this entity
    /// onto <c>SimfAppDbContext</c> (Country also lives there).</summary>
    public int NationalityId { get; set; }

    /// <summary>True when the user holds a Saudi national identity (controls
    /// which of <see cref="NationalId"/> / <see cref="IqamaNumber"/> /
    /// <see cref="PassportNumber"/> is required).</summary>
    public bool IsSaudi { get; set; }

    /// <summary>Saudi national id (10 digits) — populated when <see cref="IsSaudi"/> is true.</summary>
    public string? NationalId { get; set; }

    /// <summary>Iqama number (10 digits) — populated for non-Saudi residents.</summary>
    public string? IqamaNumber { get; set; }

    /// <summary>Passport number — populated for non-Saudi users.</summary>
    public string? PassportNumber { get; set; }

    /// <summary>H-1 — deterministic <b>blind index</b> of the normalized
    /// <see cref="NationalId"/> (keyed HMAC-SHA256, 64 hex chars). The plaintext
    /// column is encrypted with a random nonce (AesGcmPiiEncryptor) so it can never
    /// be unique-indexed or equality-queried; this stable keyed digest is what a
    /// filtered UNIQUE index enforces the no-duplicate rule on. Computed on write
    /// by the walk-in registration path; null when no National ID is set. NOT part
    /// of the SimfAppDbContext PII-encryption converter loop.</summary>
    public string? NationalIdHash { get; set; }

    /// <summary>H-1 — blind index of the normalized <see cref="IqamaNumber"/>
    /// (same scheme as <see cref="NationalIdHash"/>). Null when unset.</summary>
    public string? IqamaNumberHash { get; set; }

    /// <summary>H-1 — blind index of the normalized <see cref="PassportNumber"/>
    /// (same scheme as <see cref="NationalIdHash"/>). Null when unset.</summary>
    public string? PassportNumberHash { get; set; }


    /// <summary>D-163 (PDF §2.6) — optional job title / professional role
    /// displayed alongside the user's name (e.g. "Captain", "Director of
    /// Operations"). Free text, up to 128 chars; null when not supplied.</summary>
    public string? JobTitle { get; set; }

    /// <summary>2026-07-19 (owner) — Arabic twin of <see cref="JobTitle"/>, so a
    /// bilingual surface (e.g. the delegation head-of-delegation title) can render
    /// it in the active locale. Optional; null when not supplied.</summary>
    public string? JobTitleArabic { get; set; }

    /// <summary>
    /// B3 — D-221 (الجهة): the visitor's organisation / employer, picked from
    /// the curated <see cref="Organisation"/> lookup (the team bulk-loads it
    /// from a government Excel — D-220). Optional. Unlike <see cref="NationalityId"/>
    /// this IS a real DB FK (<c>OnDelete.Restrict</c>, mirroring the ProfileType
    /// FK) — safe because the column is nullable, so stubs simply leave it null.
    /// </summary>
    public Guid? OrganisationId { get; set; }

    /// <summary>Navigation to the picked <see cref="Organisation"/>.</summary>
    public Organisation? Organisation { get; set; }

    /// <summary>D-611 (Wave B) — the visitor's region, picked from the curated
    /// <see cref="SIMF.Domain.Regions.Region"/> lookup. Optional; a real DB FK
    /// (<c>OnDelete.Restrict</c>, same shape as <see cref="OrganisationId"/>) so
    /// the region pick is now persisted — previously the Region lookup was
    /// exposed by a picker but the choice was stored nowhere.</summary>
    public Guid? RegionId { get; set; }

    /// <summary>Navigation to the picked <see cref="SIMF.Domain.Regions.Region"/>.</summary>
    public SIMF.Domain.Regions.Region? Region { get; set; }

    /// <summary>D-758 (#10 Phase 2) — the bulk-badge generation run this placeholder
    /// profile was minted by, if any. A real intra-App-DB FK to
    /// <see cref="SIMF.Domain.Badges.BadgeBatch"/> (nullable + <c>OnDelete.Restrict</c>,
    /// same shape as <see cref="OrganisationId"/>) so a generated set can be
    /// re-emailed / revoked together. Null for every profile created through the
    /// normal sign-up / walk-in paths.</summary>
    public Guid? BadgeBatchId { get; set; }

    /// <summary>Navigation to the owning <see cref="SIMF.Domain.Badges.BadgeBatch"/>.</summary>
    public SIMF.Domain.Badges.BadgeBatch? BadgeBatch { get; set; }










    /// <summary>D-736 — whether this profile appears in "Meet People Like You"
    /// recommendations. Defaults to true; the user can opt out during sign-up
    /// or later via profile settings.</summary>
    public bool ShowInMeetLikeYou { get; set; }

    /// <summary>Saudi-format mobile number (+966xxxxxxxxx) — optional.</summary>
    public string? SaudiMobile { get; set; }

    /// <summary>International mobile (+<code>cc</code>-<code>local</code>) — optional.</summary>
    public string? InternationalMobile { get; set; }

    /// <summary>C6 — D-371 (رقم اللوحة): optional Saudi vehicle plate,
    /// stored normalized — 3 letters + 1–4 digits, ≤ 7 chars, no
    /// separators. Additive column (owner-authorised freeze lift).</summary>
    public string? PlateNumber { get; set; }

    /// <summary>D-373 — the human-friendly registration reference
    /// (<c>SIMF-2026-00000001</c>): issued once from the
    /// <c>RegistrationReferenceSequence</c> SQL sequence when the profile
    /// row is first created, unique, used for customer-facing lookup. NOT
    /// the QR id. Additive column (owner-authorised).</summary>
    public string? ReferenceNumber { get; set; }

    // -- V-1: VVIP/VIP extras for the موج (Mawj) welcome-message integration.
    //    Additive nullable columns (owner-authorised D-110 freeze lift); only
    //    populated for VVIP/VIP visitors, optional everywhere else.

    /// <summary>V-1 — the موج (Mawj) system identifier (المعرف في نظام موج).</summary>
    public string? MawjId { get; set; }

    /// <summary>V-1 — the honorific / title (اللقب), e.g. "Minister".</summary>
    public string? Honorific { get; set; }

    /// <summary>2026-07-19 (owner) — Arabic twin of <see cref="Honorific"/>, the
    /// fallback for a bilingual head-of-delegation title when JobTitle is unset.</summary>
    public string? HonorificArabic { get; set; }

    /// <summary>V-1 — preferred language as an IETF tag ("ar"/"en") for the
    /// VIP welcome message.</summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>V-1 — a separate high-resolution VIP photo (distinct from the
    /// account avatar), stored via the VIP-photo storage; the relative path.</summary>
    public string? VipPhotoRelativePath { get; set; }

    /// <summary>D-473 (#10) — true when this profile belongs to a delegation
    /// member (وفد). A delegate is an ordinary visitor with this flag set; their
    /// nationality must be an invited country (<see cref="Common.Country.IsInvited"/>).
    /// Additive column (owner-authorised freeze lift), defaults false.</summary>
    public bool IsDelegate { get; set; }

    /// <summary>Bi-Meeting rework — admin-assigned per-user eligibility to request a
    /// <b>delegation (country-to-country) meeting</b>. Replaces the old
    /// <see cref="IsDelegate"/> requester-gate: a user may now request a delegation
    /// meeting iff this flag is set, regardless of their user/profile type (the target
    /// country must still be an invited delegation — <see cref="Common.Country.IsInvited"/>).
    /// Additive column (owner-authorised freeze lift), defaults false.</summary>
    public bool AllowsDelegationMeeting { get; set; }

    /// <summary>Bi-Meeting rework — admin-assigned per-user eligibility to request a
    /// <b>speaker meeting</b>. Replaces the old VIP-only gate
    /// (<see cref="UserProfileType.AllowsVipMeetingSlots"/>): a user may now request a
    /// speaker meeting iff this flag is set, regardless of their tier (the speaker must
    /// still opt in via <c>Speaker.AllowsMeetingRequests</c>). Additive column
    /// (owner-authorised freeze lift), defaults false.</summary>
    public bool AllowsSpeakerMeeting { get; set; }

    /// <summary>
    /// The user's <see cref="ProfileType"/> when one is assigned
    /// (P8 — D-049). Null until the admin assigns one. The lookup row's
    /// <see cref="UserProfileType.UserType"/> must match the owning user's
    /// <see cref="SimfUser.UserType"/> — enforced by
    /// <c>AdminAccountService</c> at create / approve time.
    /// </summary>

    public Guid? ProfileTypeId { get; set; }

    /// <summary>Navigation to the assigned <see cref="ProfileType"/>.</summary>
    public UserProfileType? ProfileType { get; set; }
    /// <summary>
    /// The user's picked interests (P9 — D-050; الاهتمامات). M-to-M via the
    /// auto-generated <c>UserProfileInterests</c> join table. The validator
    /// requires 1-10 interests on every <c>UpsertUserProfileRequest</c>;
    /// the service rejects unknown / deactivated ids.
    /// </summary>
    public ICollection<UserInterest> Interests { get; set; } = new List<UserInterest>();

    /// <summary>
    /// D-106: short, unique, opaque event-entry identifier (decision D-046,
    /// moved here from <see cref="SimfUser"/>). Minted by
    /// <c>IQrIdMinter</c> the moment the owning user's
    /// <see cref="SimfUser.AccountState"/> transitions to
    /// <see cref="AccountState.Approved"/>. Encoded in the participant's
    /// QR code at event entry; staff scan it to check them in. Null until
    /// the account is approved. Crockford base32 alphabet (no
    /// 0/O/1/I/L/U), 12 chars (≈ 60 bits of entropy), unique across the
    /// system.
    /// </summary>
    public string? QrId { get; set; }







    /// <summary>
    /// D-106: the admin's reason for rejecting the account, in English
    /// (P10 — D-051, moved here from <see cref="SimfUser"/>). Persisted
    /// when the owning user's <see cref="SimfUser.AccountState"/>
    /// transitions to <see cref="AccountState.Rejected"/>; cleared on a
    /// later approval. Up to 500 characters, matching the
    /// <c>AdminRejectRequest.Reason</c> validator.
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// D-106: the Arabic version of <see cref="RejectionReason"/> (P10 —
    /// D-051). When the admin enters only the English reason today, the
    /// service mirrors it here as a graceful fallback (R1 default); a
    /// future bilingual admin form may diverge the two.
    /// </summary>
    public string? RejectionReasonArabic { get; set; }



    /////WHERE STATUS? APPROVED OR REJECTED OR UNDER REVIEW?//////////////////////
    /////set create and update by and update date


    /// <summary>The relative path of the encrypted ID-image file, under the
    /// file store's <c>IdDocument</c> folder. Null when no image has been
    /// uploaded. The bytes are envelope-encrypted in the unified store rooted
    /// at <c>FileStorage:RootPath</c>.</summary>
    public string? IdImageRelativePath { get; set; }

    // -- `accessibility-server-sync`: the five accessibility choices the app
    //    used to keep in device prefs ONLY, so they never followed the user to a
    //    second device and did not survive a reinstall. They are per-account
    //    settings, so they live on the profile row (which already carries the
    //    bare UserId — D-157, no cross-DB FK). Served by
    //    GET / PUT /app/account/preferences.
    //    Tests: SIMF.Api.Tests/AccountPreferencesTests.cs

    /// <summary>The default text-size name — the value a profile row carries
    /// until the user picks another. Must stay equal to
    /// <c>SIMF.Contracts.Account.AccountPreferences.DefaultTextSize</c>; the
    /// Domain project does not reference Contracts, so the two constants are
    /// pinned together by a test rather than by the compiler.</summary>
    public const string DefaultAccessibilityTextSize = "normal";

    /// <summary>The chosen text size, stored as the app's <b>stable enum name</b>
    /// (<c>small</c> / <c>normal</c> / <c>large</c> / <c>extraLarge</c>) and NOT
    /// as an index, so reordering the client enum can never re-interpret stored
    /// rows. Case-sensitive: the app matches the name byte for byte.</summary>
    public string AccessibilityTextSize { get; set; } = DefaultAccessibilityTextSize;

    /// <summary>High-contrast rendering (تباين عالٍ). Off by default.</summary>
    public bool AccessibilityHighContrast { get; set; }

    /// <summary>Suppress non-essential animation (تقليل الحركة). Off by default.</summary>
    public bool AccessibilityReduceMotion { get; set; }

    /// <summary>Announce each screen through the platform accessibility channel
    /// (قارئ الشاشة). Off by default.</summary>
    public bool AccessibilityScreenReaderAssist { get; set; }

    /// <summary>Show the live / session caption strip (الترجمة النصية). <b>On</b>
    /// by default — the one accessibility choice that starts enabled.</summary>
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