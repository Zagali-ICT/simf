// Tests: SIMF.Api.Tests/UserProfileTests.cs (the collapsed MobileNumber column
//        is filled from either shipped wire field, and both keys still
//        round-trip over the real HTTP surface)
//        SIMF.Api.Tests/AdminAccountMobileTests.cs (the desk edit writes it too)
using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Organisations;
using SIMF.Domain.Files;

namespace SIMF.Domain.Profiles;

/// <summary>
/// The attendee record, and the primary one: a person who attends the forum has a
/// profile, whether or not they ever hold an account. Admin-typed users carry no
/// profile.
///
/// <para><see cref="ProfileTypeId"/> hangs off the profile rather than off the
/// user because the subtype is a property of the profile, not of the identity:
/// an audience tier (VVIP, VIP, Gold) or a partner kind (Staff, Exhibitor,
/// Sponsor), told apart by <see cref="UserProfileType.IsForVisitor"/>.</para>
/// </summary>
public class UserProfile : BaseAuditEntity
{
    /// <summary>
    /// The Identity account that can sign in as this attendee, or null when there
    /// is none. A bare Guid rather than a navigation: the user row lives in the
    /// Identity database, so there is no foreign key across the two and the
    /// service layer enforces the link on write.
    ///
    /// <para>NULL is the ordinary case for someone who attends and never installs
    /// the app — a walk-in, or a badge minted into a bulk order. An account is
    /// created and linked here only when its owner wants mobile access, which is
    /// the sole thing it grants: admission is decided by
    /// <see cref="AdmissionState"/> on this row, never by the user's state.</para>
    ///
    /// <para>The unique index on this column is FILTERED to non-null rows. SQL
    /// Server treats NULLs as equal in a unique index, so an unfiltered one would
    /// permit exactly ONE profile without an account across the whole system —
    /// which passes every test and then fails on the second walk-in of the
    /// event. Never write <see cref="Guid.Empty"/> here as a stand-in for "none":
    /// it is already in use elsewhere as a "matches nobody" sentinel, and a row
    /// carrying it would collide with every other such row on the identity,
    /// seating and exhibitor-lead lookups.</para>
    /// </summary>
    public Guid? UserId { get; set; }

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

    /// <summary>Chooses which identity document a registrant must supply — a
    /// Saudi's national id, or a non-Saudi's Iqama and/or passport. It records
    /// what is REQUIRED, which is why it survived the collapse of the three
    /// number columns: <see cref="ProfileIdentityDocument.Kind"/> records what was
    /// actually supplied, and the two answer different questions.
    ///
    /// <para>Not derived from <see cref="NationalityId"/> even though a Saudi is
    /// nationality 682. Two write paths legitimately set this flag with no
    /// nationality at all — quick-register leaves <see cref="NationalityId"/> at
    /// 0, and the offline badge upload sends an empty country code — so deriving
    /// it would mis-classify exactly the desk-captured rows that have the least
    /// data to spare. It is also on the shipped app wire as
    /// <c>isSaudi</c>.</para>
    ///
    /// <para>Not derived, but not free to contradict either: where a nationality
    /// WAS captured, the two must agree (see
    /// <see cref="SaudiNationalityId"/>).</para></summary>
    public bool IsSaudi { get; set; }

    /// <summary>ISO 3166-1 numeric for Saudi Arabia, the <c>SA</c> row of the
    /// Country lookup, and the only <see cref="NationalityId"/> that may sit
    /// beside <see cref="IsSaudi"/> = true.
    ///
    /// <para>The pairing is checked where a nationality is captured, and cannot
    /// be a database CHECK: the two desk paths that legitimately write
    /// <see cref="IsSaudi"/> with <see cref="NationalityId"/> 0 would fail
    /// it.</para></summary>
    public const int SaudiNationalityId = 682;

    /// <summary>
    /// Every identity document this attendee holds — one row per document,
    /// each carrying its own encrypted number and deterministic digest, and the
    /// ONLY place those numbers live.
    ///
    /// <para>It replaced three columns per number (a plaintext one and a
    /// blind-index one for each of national id, Iqama and passport) because an
    /// attendee can hold MORE THAN ONE document at once — the upsert validator's
    /// "either Iqama or Passport" is an OR, not an XOR — and because a single
    /// unique index over every digest catches a CROSS-KIND duplicate, which no
    /// arrangement of three per-kind filtered indexes can see.</para>
    ///
    /// <para>Load it before writing through it: an unloaded navigation looks
    /// empty, and a sync against an empty collection inserts a second copy of
    /// every document the attendee already has.</para>
    /// </summary>
    public ICollection<ProfileIdentityDocument> IdentityDocuments { get; set; } =
        new List<ProfileIdentityDocument>();

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

    /// <summary>
    /// The employer the visitor typed when theirs is not in the curated lookup,
    /// captured alongside <see cref="OrganisationId"/> pointing at the seeded
    /// <see cref="Organisation.OtherId"/> row.
    ///
    /// <para>Both halves are kept on purpose. Without the free text, "not in the
    /// list" was a dead end: the picker said no matches and the visitor could
    /// not proceed, because organisation is a required field on the form.
    /// Without the lookup row, every existing join, grid and export
    /// over <c>OrganisationId</c> would have had to learn a second, nullable
    /// path — so the id stays populated and reporting keeps working unchanged,
    /// while this column carries what the person actually wrote.</para>
    ///
    /// <para>Deliberately NOT a route into the lookup table. Letting the app
    /// create an Organisation on the fly is the obvious third option and it
    /// fills a curated, government-sourced list with "google", "Google" and
    /// "GOOGLE Inc". These rows are read as free text and can be reconciled by
    /// a human later.</para>
    /// </summary>
    public string? OrganisationOther { get; set; }

    /// <summary>The visitor's region, picked from the curated
    /// <see cref="SIMF.Domain.Regions.Region"/> lookup. Optional, and a real
    /// foreign key of the same shape as <see cref="OrganisationId"/>.</summary>
    public Guid? RegionId { get; set; }

    public SIMF.Domain.Regions.Region? Region { get; set; }

    /// <summary>The order this attendee arrived on, so a set handed out together
    /// can be topped up, re-emailed or revoked together. REQUIRED: everyone
    /// belongs to an order, and whoever arrived without a bulk one behind them —
    /// a direct registration, a walk-in, an exhibition-desk capture — belongs to
    /// the seeded <see cref="Badges.BadgeBatch.DirectRegistrationId"/>.
    ///
    /// <para>It was nullable and set only by the bulk mint, which meant "which
    /// order did this attendee come from" had no answer at all for anyone who
    /// registered themselves.</para></summary>
    public Guid BadgeBatchId { get; set; } = SIMF.Domain.Badges.BadgeBatch.DirectRegistrationId;

    public SIMF.Domain.Badges.BadgeBatch? BadgeBatch { get; set; }

    /// <summary>The edition year this attendee's badge is valid in. Stamped from
    /// the open edition at creation, and re-stamped to the new year for anyone
    /// carried forward when the next one opens.
    ///
    /// <para>Closing a year does not delete its attendees — their records stay,
    /// labelled with the year they belong to, which is what makes an edition a
    /// queryable dimension rather than a date range. Opening the next year clears
    /// the QR of everyone holding one and moves this column with it, so a
    /// returning attendee is re-issued rather than left holding a badge that
    /// every door refuses. The two have to move together: the gate refuses a
    /// badge whose year is not the open one, so re-issuing against a stale year
    /// would hand out badges that are dead on arrival.</para></summary>
    public int EditionYear { get; set; }

    /// <summary>Whether this profile appears in the "Meet People Like You"
    /// recommendations and the partner directory. The column defaults to true, so
    /// a row stays visible until the user opts out, and the admin-side master
    /// switch <see cref="UserProfileType.ShowInPartnerDirectory"/> is ANDed with
    /// it.</summary>
    public bool ShowInMeetLikeYou { get; set; }

    /// <summary>The attendee's ONE mobile number, in canonical E.164
    /// (<c>+966501234567</c>, <c>+447700900123</c>) — the single column that
    /// supersedes <see cref="SaudiMobile"/> and
    /// <see cref="InternationalMobile"/>.
    ///
    /// <para>A Saudi mobile IS an international mobile with <c>+966</c> on the
    /// front, so the two columns were never two attributes. Two columns let a row
    /// hold two DIFFERENT numbers with nothing on the row saying which one to
    /// ring, forced every reader to coalesce, and de-duplicated against nothing —
    /// the same person's number stored <c>0501234567</c> in one row and
    /// <c>+966501234567</c> in another looked like two people. This column holds
    /// the folded form (the Saudi local <c>05XXXXXXXX</c> spelling becomes
    /// <c>+9665XXXXXXXX</c>), so one number is one string.</para>
    ///
    /// <para>Written only through <c>ProfileMobileStorage.Sync</c>, which sets
    /// this column and the two superseded ones together, so a row can never say
    /// two things.</para></summary>
    public string? MobileNumber { get; set; }

    /// <summary>SUPERSEDED by <see cref="MobileNumber"/>. Still written, in exact
    /// lockstep, and still the only thing every reader projects — which is why it
    /// cannot go yet.
    ///
    /// <para>Populated when the canonical number is Saudi (<c>+966…</c>) and NULL
    /// otherwise; <see cref="InternationalMobile"/> is its exact complement, so
    /// at most one of the pair is ever set. Saudi wins when both arrive, matching
    /// the precedence the VIP roster already displays with
    /// (<c>VipRosterService</c> picks <c>SaudiMobile ?? InternationalMobile</c>).</para>
    ///
    /// <para><b>Readers that must move to <see cref="MobileNumber"/> before this
    /// pair can be dropped:</b> <c>UserProfileService.ToResponse</c> (the
    /// <c>saudiMobile</c> / <c>internationalMobile</c> wire keys the shipped app
    /// decodes — those two JSON names are append-only and must keep being emitted
    /// and accepted whatever the storage does), <c>VipRosterService</c>,
    /// <c>MyAreaService</c>, <c>AdminApprovalReadService</c>,
    /// <c>VisitorShareService</c> (+ the vCard endpoint),
    /// <c>ExhibitorVisitorService</c>, <c>OfflineBadgeUploadService</c>,
    /// <c>AdminAccountService</c>, <c>AiAuditDetail</c>, and the Control-Panel
    /// pages that bind them (Pending/View/Edit for visitors and others, the
    /// walk-in form, the VIP export). Legacy rows written before this column
    /// existed carry the pair and a NULL <see cref="MobileNumber"/>, so a reader
    /// that switches early would blank a value that has always been
    /// populated.</para></summary>
    public string? SaudiMobile { get; set; }

    /// <summary>SUPERSEDED by <see cref="MobileNumber"/> — see the remarks on
    /// <see cref="SaudiMobile"/>. Populated when the canonical number is NOT
    /// Saudi, and NULL when it is.</summary>
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

    /// <summary>A separate high-resolution VIP photo, distinct from the account
    /// avatar, as its row in the unified file store. A real foreign key, unlike
    /// the avatar: both this row and <c>StoredFile</c> live in the App database,
    /// so the constraint is legal here and the database keeps it honest.
    /// Null for everyone who is not VVIP or VIP.</summary>
    public Guid? VipPhotoFileId { get; set; }

    public StoredFile? VipPhotoFile { get; set; }
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
    /// (<see cref="UserProfileType.IsVipTier"/>) as the requester
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
    /// Whether this attendee may be admitted, and the authority on that question
    /// for every gate, hall door and badge in the system.
    ///
    /// <para>This is the SAME fact that used to live on
    /// <see cref="IdentityAccess.SimfUser.AccountState"/>, MOVED here rather than
    /// copied. It had to move because an attendee need not have an account
    /// (<see cref="UserId"/>), and a fact that only exists for some attendees
    /// cannot be the one the gate reads. It must never be mirrored back onto the
    /// user row: the two databases have no distributed transaction between them,
    /// so a second writable copy would drift, and one of the two would then be
    /// deciding admission while the other looked authoritative.</para>
    ///
    /// <para><see cref="AccountState.Registered"/> and
    /// <see cref="AccountState.EmailVerified"/> describe a credential flow and so
    /// belong to an account, not to an attendee; a profile therefore starts at
    /// <see cref="AccountState.PendingApproval"/> and moves to
    /// <see cref="AccountState.Approved"/> or <see cref="AccountState.Rejected"/>.
    /// The enum is shared with the user row because it is the same vocabulary,
    /// not because the two columns track each other.</para>
    /// </summary>
    public AccountState AdmissionState { get; set; } = AccountState.PendingApproval;

    /// <summary>When <see cref="AdmissionState"/> last changed. Saudi wall clock.</summary>
    public DateTime? StateChangedAt { get; set; }

    /// <summary>The admin who last changed <see cref="AdmissionState"/>, or null
    /// when nobody has — a self-service registrant reaching PendingApproval has no
    /// actor, and neither does a seeded row. A bare Guid into the Identity
    /// database, like <see cref="UserId"/>.</summary>
    public Guid? StateChangedByUserId { get; set; }

    /// <summary>
    /// The short, opaque event-entry identifier carried in the participant's QR
    /// code, which staff scan to check them in. Minted by <c>IQrIdMinter</c> the
    /// moment <see cref="AdmissionState"/> reaches
    /// <see cref="AccountState.Approved"/>, so it stays null until the attendee is
    /// approved. Twelve characters of the Crockford base32 alphabet (no 0, O, 1,
    /// I, L or U), unique across the system.
    /// </summary>
    public string? QrId { get; set; }

    /// <summary>
    /// The admin's reason for rejecting the attendee, written when
    /// <see cref="AdmissionState"/> reaches
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

    /// <summary>The registrant's ID document, as its row in the unified file
    /// store. A real foreign key: both sides live in the App database. The bytes
    /// are envelope-encrypted and never sit in this row. Null when nothing has
    /// been uploaded.
    ///
    /// <para>This was <c>IdImageRelativePath</c>, and its comment described a
    /// path "inside the unified store rooted at <c>FileStorage:RootPath</c>,
    /// under its <c>IdDocument</c> folder" long after the column had stopped
    /// holding anything of the sort.</para></summary>
    public Guid? IdImageFileId { get; set; }

    public StoredFile? IdImageFile { get; set; }
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
