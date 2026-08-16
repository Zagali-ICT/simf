using SIMF.Common.Enums;

namespace SIMF.Domain.Notifications;

/// <summary>
/// A manual announcement composed at the Control Panel's broadcast desk. The API inserts
/// one pending row and a background worker fans it out, writing an in-app notification
/// and a queued email per recipient, then stamps the counters and status here. Recipients
/// are resolved from the Identity database at send time and never copied onto this row.
/// </summary>
public sealed class NotificationBroadcast
{
    public Guid Id { get; set; }

    public Guid CreatedByUserId { get; set; }

    public BroadcastTargetMode TargetMode { get; set; }

    /// <summary>Set for a session broadcast. No navigation, so the broadcast survives the session's removal.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>Set for an audience broadcast.</summary>
    public BroadcastAudienceScope? AudienceScope { get; set; }

    public string Title { get; set; } = string.Empty;
    public string TitleArabic { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string BodyArabic { get; set; } = string.Empty;

    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;

    public BroadcastStatus Status { get; set; } = BroadcastStatus.Pending;

    // Counters, all zero until the worker has processed the row.
    public int TotalRecipients { get; set; }
    public int Dispatched { get; set; }

    /// <summary>Recipients with an address on file.</summary>
    public int EmailsEnqueued { get; set; }

    /// <summary>Recipients whose dispatch threw.</summary>
    public int Skipped { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? Error { get; set; }
}
