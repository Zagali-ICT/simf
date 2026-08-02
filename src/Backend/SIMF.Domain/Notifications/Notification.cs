using SIMF.Common.Enums;

namespace SIMF.Domain.Notifications;

/// <summary>
/// One in-app notification for one user (P12 — D-053). Written by
/// <c>INotificationDispatcher</c> from every account-lifecycle event
/// (profile submitted, approved, rejected, 2FA reset, password
/// changes, etc. — wired in P13 — D-054). Bilingual in the row so the
/// page picks by current culture without a second lookup.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; set; }

    /// <summary>The user this notification belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Stable event-kind used by the UI for icons + by SOC for
    /// filtering. D-108: persisted as the enum name string via the
    /// EF value converter on <c>NotificationConfiguration</c>.
    /// </summary>
    public NotificationKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>Visual + audio tier — Info / Success / Warning / Error.</summary>
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>Null until the user marks it read.</summary>
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Optional pointer to the entity that triggered the row —
    /// e.g. "UserProfile" / "Account" / "Interest". Lets a future
    /// notification-tile deep-link to the right page.</summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>Optional foreign-key into the related entity.</summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>D-677 — an app-internal deep-link the tile opens on tap (e.g.
    /// <c>/rate?code=Session&amp;targetId=…</c> or <c>/badge</c>). Populated from
    /// <c>NotificationKindCatalog</c> when the dispatch request leaves it null.
    /// Null = the tile is informational (no navigation).</summary>
    public string? ClickUrl { get; set; }

    /// <summary>D-677 — a stable group code the app sections the list by
    /// (Sessions / Bookings / Meetings / Ratings / Account / Vip). Populated from
    /// the catalog when the request leaves it null.</summary>
    public string? GroupCode { get; set; }

    /// <summary>
    /// D-108: derived read flag — true once <see cref="ReadAt"/> is set.
    /// Ignored by EF so it doesn't sneak into a SELECT projection; the
    /// repository materialises an equivalent flag into the DTO.
    /// </summary>
    public bool IsRead => ReadAt is not null;
}
