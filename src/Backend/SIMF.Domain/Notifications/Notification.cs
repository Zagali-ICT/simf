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
    /// Stable event-kind name (e.g. <c>"Account.Approved"</c>,
    /// <c>"Admin.PendingVisitor"</c>) used by the UI for icons + by SOC
    /// for filtering. Defined alongside <c>AuditEvents</c>.
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    /// <summary>Visual + audio tier — Info / Success / Warning / Error.</summary>
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    /// <summary>Null until the user marks it read.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Optional pointer to the entity that triggered the row —
    /// e.g. "UserProfile" / "Account" / "Interest". Lets a future
    /// notification-tile deep-link to the right page.</summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>Optional foreign-key into the related entity.</summary>
    public Guid? RelatedEntityId { get; set; }
}

/// <summary>The visual + audio tier of a notification (P12 — D-053).</summary>
public enum NotificationSeverity
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3,
}
