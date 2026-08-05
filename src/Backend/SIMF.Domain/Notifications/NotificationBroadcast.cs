using SIMF.Common.Enums;

namespace SIMF.Domain.Notifications;

/// <summary>
/// A manual announcement composed at the Control Panel's broadcast desk. The API
/// inserts a single pending row and a background worker fans it out, writing one
/// in-app notification and one queued email per recipient, then stamps the
/// counters and status here.
///
/// <para>Recipients are resolved from the Identity database at send time and are
/// never copied onto this row.</para>
/// </summary>
public sealed class NotificationBroadcast
{
    public Guid Id { get; set; }

    /// <summary>The admin who composed it. A bare Guid: the user lives in the
    /// Identity database.</summary>
    public Guid CreatedByUserId { get; set; }

    public BroadcastTargetMode TargetMode { get; set; }

    /// <summary>Set for a session broadcast, null for an audience one. There is
    /// no navigation, so the broadcast survives as history even after the session
    /// is removed.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>Set for an audience broadcast, null for a session one.</summary>
    public BroadcastAudienceScope? AudienceScope { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;

    // Counters, all zero until the worker has processed the row.

    /// <summary>Distinct recipients the worker resolved.</summary>
    public int TotalRecipients { get; set; }

    /// <summary>In-app notifications successfully written.</summary>
    public int Dispatched { get; set; }

    /// <summary>Emails enqueued, meaning recipients with an address on file.</summary>
    public int EmailsEnqueued { get; set; }

    /// <summary>Recipients whose dispatch threw.</summary>
    public int Skipped { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>When the worker claimed the row.</summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>When the fan-out finished, whether it completed or failed.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>Failure detail, set only when the broadcast failed.</summary>
    public string? Error { get; set; }
}
