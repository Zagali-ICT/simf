namespace SIMF.Domain.Auditing;

/// <summary>
/// One entry in the operation log — the durable audit trail of
/// security-relevant events (SIMF-FDS-001 section 9). The operation log is
/// append-only.
/// </summary>
public class OperationLogEntry
{
    public Guid Id { get; set; }

    /// <summary>When the event occurred (UTC).</summary>
    public DateTimeOffset TimestampUtc { get; set; }

    /// <summary>The stable event-type name (see <c>AuditEvents</c>).</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>Whether the operation succeeded or failed.</summary>
    public AuditOutcome Outcome { get; set; }

    /// <summary>The email address the event concerns; null if not applicable.</summary>
    public string? SubjectEmail { get; set; }

    /// <summary>The user the event concerns; null if not applicable.</summary>
    public Guid? SubjectUserId { get; set; }

    /// <summary>The client IP the request came from.</summary>
    public string? SourceIp { get; set; }

    /// <summary>The client user-agent.</summary>
    public string? UserAgent { get; set; }

    /// <summary>The request correlation id, for stitching multi-step activity.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>The API error code, when the event is a failure.</summary>
    public string? ErrorCode { get; set; }

    /// <summary>Optional extra detail; never a secret.</summary>
    public string? Detail { get; set; }
}
