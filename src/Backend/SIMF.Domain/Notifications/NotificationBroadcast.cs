using SIMF.Common.Enums;

namespace SIMF.Domain.Notifications;

/// <summary>
/// A manual admin notification broadcast composed in the Control Panel
/// "Announcements" desk. The API inserts one <see cref="BroadcastStatus.Pending"/>
/// row; the background <c>NotificationBroadcastWorker</c> fans it out (one in-app
/// notification row + one queued email per recipient) and stamps the counters +
/// status. Lives on SIMF_App; recipients are resolved across the D-157 boundary
/// at send time and are never copied onto this row.
/// </summary>
public sealed class NotificationBroadcast
{
    public Guid Id { get; set; }

    /// <summary>The admin who composed it — logical FK to <c>SimfUser.Id</c> on the
    /// Identity DB (no cross-DB constraint, D-157).</summary>
    public Guid CreatedByUserId { get; set; }

    public BroadcastTargetMode TargetMode { get; set; }

    /// <summary>Set when <see cref="TargetMode"/> is
    /// <see cref="BroadcastTargetMode.Session"/> — logical FK to <c>Session.Id</c>
    /// (no navigation, so the broadcast survives as history even if the session is
    /// later removed). Null for an audience broadcast.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>Set when <see cref="TargetMode"/> is
    /// <see cref="BroadcastTargetMode.Audience"/>. Null for a session broadcast.</summary>
    public BroadcastAudienceScope? AudienceScope { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;

    /// <summary>Distinct recipients the worker resolved; 0 until processed.</summary>
    public int TotalRecipients { get; set; }

    /// <summary>In-app notification rows successfully dispatched.</summary>
    public int Dispatched { get; set; }

    /// <summary>Emails enqueued (recipients with an email on file).</summary>
    public int EmailsEnqueued { get; set; }

    /// <summary>Recipients skipped (a dispatch that threw).</summary>
    public int Skipped { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the worker claimed the row (moved it to Processing).</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When the fan-out finished (Completed or Failed).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Failure detail when <see cref="Status"/> is
    /// <see cref="BroadcastStatus.Failed"/>; null otherwise. ≤1024 chars.</summary>
    public string? Error { get; set; }
}
