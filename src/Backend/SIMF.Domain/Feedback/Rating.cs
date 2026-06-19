using SIMF.Domain.Common;

namespace SIMF.Domain.Feedback;

/// <summary>
/// D-199 (Mockup screen 40 "Rate the Forum") — a single overall forum
/// rating per attendee, plus the four optional per-element scores (D-463,
/// Figma 1116:16894 "قيّم العناصر"). Per the owner default each user has at
/// most one rating: the unique index on <see cref="UserId"/> enforces
/// "one rating per user", and the rate endpoint upserts onto it.
/// <para>
/// Derives from <see cref="BaseAuditEntity"/> for the standard audit/soft-delete
/// stamps (<c>CreatedBy</c>/<c>UpdatedBy</c>/<c>CreatedAt</c>/<c>UpdatedAt</c>/
/// <c>IsActive</c>); the owning attendee is <see cref="UserId"/> (not a separate
/// actor). The upsert flips a previously soft-deleted row back to active.
/// </para>
/// </summary>
public sealed class Rating : BaseAuditEntity
{

    /// <summary>The attendee who submitted the rating
    /// (<c>SimfUser.Id</c> / <c>sub</c> claim). Unique — one row per user.</summary>
    public Guid UserId { get; set; }

    /// <summary>Overall experience score, 1–5 inclusive. Validated at the
    /// edge (FluentValidation) and re-checked in the service.</summary>
    public int Stars { get; set; }

    /// <summary>Optional per-element score for the forum organisation
    /// ("التنظيم"), 1–5 inclusive. Null when the attendee skips this element.
    /// Additive nullable column (Figma 1116:16894 "قيّم العناصر").</summary>
    public int? OrganizationStars { get; set; }

    /// <summary>Optional per-element score for the forum content
    /// ("المحتوى"), 1–5 inclusive. Null when skipped.</summary>
    public int? ContentStars { get; set; }

    /// <summary>Optional per-element score for the app
    /// ("التطبيق"), 1–5 inclusive. Null when skipped.</summary>
    public int? AppStars { get; set; }

    /// <summary>Optional per-element score for the venue &amp; facilities
    /// ("المكان والمرافق"), 1–5 inclusive. Null when skipped.</summary>
    public int? VenueStars { get; set; }

    /// <summary>Optional free-text comment ("Comments (optional)" on the
    /// mockup). Null when the attendee submits stars only.</summary>
    public string? Comment { get; set; }
}
