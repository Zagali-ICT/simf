namespace SIMF.Domain.IdentityAccess;

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
/// Sponsor / …); the discriminator on <see cref="ProfileType.UserType"/>
/// keeps each lookup row scoped to a single user kind.</para>
///
/// <para>The ID-image attachment is stored on disk encrypted-at-rest via
/// <c>IUserIdDocumentStorage</c>. The relative path lives here; the bytes
/// never sit in the row.</para>
/// </summary>
public class UserProfile
{
    /// <summary>Surrogate id — separate from <see cref="UserId"/> so the
    /// row can be re-created if the user is recovered.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The owning user.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The user's <see cref="ProfileType"/> when one is assigned
    /// (P8 — D-049). Null until the admin assigns one. The lookup row's
    /// <see cref="ProfileType.UserType"/> must match the owning user's
    /// <see cref="SimfUser.UserType"/> — enforced by
    /// <c>AdminAccountService</c> at create / approve time.
    /// </summary>
    public Guid? ProfileTypeId { get; set; }

    /// <summary>Navigation to the assigned <see cref="ProfileType"/>.</summary>
    public ProfileType? ProfileType { get; set; }

    /// <summary>
    /// The user's picked interests (P9 — D-050; الاهتمامات). M-to-M via the
    /// auto-generated <c>UserProfileInterests</c> join table. The validator
    /// requires 1-10 interests on every <c>UpsertUserProfileRequest</c>;
    /// the service rejects unknown / deactivated ids.
    /// </summary>
    public ICollection<Interest> Interests { get; set; } = new List<Interest>();

    /// <summary>Full name in Arabic.</summary>
    public string ArabicName { get; set; } = string.Empty;

    /// <summary>Full name in English exactly as printed in the passport.</summary>
    public string EnglishName { get; set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 nationality code (e.g. "SA", "AE", "US").</summary>
    public string NationalityCode { get; set; } = string.Empty;

    /// <summary>Date of birth (date only).</summary>
    public DateOnly? DateOfBirth { get; set; }

    /// <summary>Place of birth — free text, up to 128 chars.</summary>
    public string PlaceOfBirth { get; set; } = string.Empty;

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

    /// <summary>Saudi-format mobile number (+966xxxxxxxxx) — optional.</summary>
    public string? SaudiMobile { get; set; }

    /// <summary>International mobile (+<code>cc</code>-<code>local</code>) — optional.</summary>
    public string? InternationalMobile { get; set; }

    /// <summary>The relative path of the encrypted ID-image file on disk,
    /// under the configured <c>Storage:UserIdDocumentBase</c>. Null when no
    /// image has been uploaded. The bytes are AES-GCM encrypted with the
    /// per-installation key — see <c>EncryptedUserIdDocumentStorage</c>.</summary>
    public string? IdImageRelativePath { get; set; }

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

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the row was last updated (UTC); null on first save.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
