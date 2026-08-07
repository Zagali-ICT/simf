using SIMF.Common.Enums;

namespace SIMF.Domain.Notifications;

/// <summary>
/// One in-app notification for one user, written by the notification dispatcher
/// from account-lifecycle events such as profile submitted, approved, rejected,
/// a two-factor reset or a password change. Both languages are held on the row,
/// so the page picks by current culture without a second lookup.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>The event kind, used for the tile icon and for filtering.
    /// Persisted as the enum name rather than its integer, so appending a value
    /// cannot renumber the stored rows.</summary>
    public NotificationKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>Null until the user marks it read.</summary>
    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>What triggered the row, such as "UserProfile" or "Account", so a
    /// tile can deep-link to the right page.</summary>
    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    /// <summary>An app-internal link the tile opens on tap. Filled from the
    /// notification-kind catalogue when the dispatch request leaves it null; null
    /// after that means the tile is informational and does not navigate.</summary>
    public string? ClickUrl { get; set; }

    /// <summary>The group the app sections the list by — Sessions, Bookings,
    /// Meetings, Ratings, Account or VIP. Filled from the catalogue when the
    /// request leaves it null.</summary>
    public string? GroupCode { get; set; }

    /// <summary>Ignored by EF so it cannot reach a SELECT projection; the
    /// repository materialises the equivalent flag into the DTO.</summary>
    public bool IsRead => ReadAt is not null;
}
