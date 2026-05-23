namespace SIMF.Domain.IdentityAccess;

/// <summary>
/// A visitor's profile — the per-attendee information captured at
/// registration (decision D-046, myComment #18). One row per
/// <see cref="SimfUser"/>; null until the visitor fills the form.
///
/// <para>The ID-image attachment is stored on disk encrypted-at-rest via
/// <c>IVisitorIdStorage</c>. The relative path lives here; the bytes
/// never sit in the row.</para>
/// </summary>
public class VisitorProfile
{
    /// <summary>Surrogate id — separate from <see cref="UserId"/> so the
    /// row can be re-created if the user is recovered.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The owning user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Visitor type — "Visitor" / "Exhibitor" / "Press". Persisted as a string for forward-compatibility.</summary>
    public string VisitorType { get; set; } = "Visitor";

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

    /// <summary>True when the visitor holds a Saudi national identity (controls
    /// which of <see cref="NationalId"/> / <see cref="IqamaNumber"/> /
    /// <see cref="PassportNumber"/> is required).</summary>
    public bool IsSaudi { get; set; }

    /// <summary>Saudi national id (10 digits) — populated when <see cref="IsSaudi"/> is true.</summary>
    public string? NationalId { get; set; }

    /// <summary>Iqama number (10 digits) — populated for non-Saudi residents.</summary>
    public string? IqamaNumber { get; set; }

    /// <summary>Passport number — populated for non-Saudi visitors.</summary>
    public string? PassportNumber { get; set; }

    /// <summary>Saudi-format mobile number (+966xxxxxxxxx) — optional.</summary>
    public string? SaudiMobile { get; set; }

    /// <summary>International mobile (+<code>cc</code>-<code>local</code>) — optional.</summary>
    public string? InternationalMobile { get; set; }

    /// <summary>The relative path of the encrypted ID-image file on disk,
    /// under the configured <c>Storage:VisitorIdBase</c>. Null when no
    /// image has been uploaded. The bytes are AES-GCM encrypted with the
    /// per-installation key — see <c>EncryptedVisitorIdStorage</c>.</summary>
    public string? IdImageRelativePath { get; set; }

    /// <summary>When the row was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the row was last updated (UTC); null on first save.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }
}
