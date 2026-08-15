using SIMF.Common.Enums;

namespace SIMF.Domain.Notifications;

/// <summary>One in-app notification for one user, written by the notification dispatcher. Both languages sit on the row, so the page picks by culture without a second lookup.</summary>
public sealed class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    /// <summary>Persisted by enum name, so appending a value cannot renumber stored rows.</summary>
    public NotificationKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public DateTime? ReadAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? RelatedEntityType { get; set; }

    public Guid? RelatedEntityId { get; set; }

    /// <summary>An app-internal link, filled from the notification-kind catalogue when the dispatch request leaves it null; still null means the tile does not navigate.</summary>
    public string? ClickUrl { get; set; }

    /// <summary>Sections the app's list: Sessions, Bookings, Meetings, Ratings, Account or VIP. Also filled from the catalogue.</summary>
    public string? GroupCode { get; set; }

    public bool IsRead => ReadAt is not null;
}
