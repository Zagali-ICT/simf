namespace SIMF.Contracts.Contacts;

// Visitor-to-visitor contact sharing
// (app audience). A visitor shares their own card by showing a QR built from
// their share token; another visitor resolves that token to a live card and
// can save it to "My Contacts". No Identity-owned data is duplicated — cards
// resolve on read from the subject's UserProfile + a permitted email round-trip.

/// <summary>The caller's own share token (minted on first request). The mobile
/// client renders it as a QR / share-intent code.</summary>
public sealed record VisitorShareTokenResponse(string Token);

/// <summary>Body of <c>POST /app/contacts/resolve</c> — the scanned token.</summary>
public sealed class ResolveContactRequest
{
    public string Token { get; set; } = string.Empty;
}

/// <summary>Body of <c>POST /app/contacts/save</c> — the scanned token plus an
/// optional note the owner attaches to the saved contact.</summary>
public sealed class SaveContactRequest
{
    public string Token { get; set; } = string.Empty;
    public string? Note { get; set; }
}

/// <summary>A visitor's contact card — projected <b>live</b> from their
/// <c>UserProfile</c> (+ Organisation / Country lookups + a permitted email
/// round-trip, OI-2). No photo in V1 (the encrypted ID document is never
/// exposed). When the subject is unavailable (deactivated / no profile),
/// <see cref="Available"/> is false and the name fields are blank.</summary>
public sealed record VisitorCard(
    Guid UserId,
    string Name,
    string NameArabic,
    string? JobTitle,
    // 2026-07-20 — Arabic twin of JobTitle (UserProfile.JobTitleArabic), paired
    // like Organisation/OrganisationArabic so the app localizes the title.
    string? JobTitleArabic,
    string? Organisation,
    string? OrganisationArabic,
    string? Email,
    string? SaudiMobile,
    string? InternationalMobile,
    int? CountryId,
    string? CountryName,
    string? CountryNameArabic,
    bool Available,
    // Appended (append-only wire): the subject's attendee record. The exhibitor
    // lead paths fill it, because a captured lead is keyed by profile and its
    // subject may hold no account at all — in which case UserId above, a SHIPPED
    // non-nullable field, carries Guid.Empty and must never be used to look the
    // subject up. Null on the contact-share paths, which are account-to-account.
    Guid? UserProfileId = null);

/// <summary>One row in the caller's <em>My Contacts</em> list — resolved on
/// read from the subject's profile (no stored PII snapshot).</summary>
public sealed record SavedContactRow(
    Guid Id,
    Guid SubjectUserId,
    string Name,
    string NameArabic,
    string? JobTitle,
    // 2026-07-20 — Arabic twin of JobTitle so the saved-contact tile localizes.
    string? JobTitleArabic,
    string? Organisation,
    string? Note,
    DateTime SavedAt,
    bool SubjectAvailable);
