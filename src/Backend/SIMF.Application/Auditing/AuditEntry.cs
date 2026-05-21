using SIMF.Domain.Auditing;

namespace SIMF.Application.Auditing;

/// <summary>
/// The business fields of an audit event. The request-context fields — source
/// IP, user-agent, correlation id — are added by the audit-log implementation.
/// </summary>
public sealed record AuditEntry
{
    public required string EventType { get; init; }

    public required AuditOutcome Outcome { get; init; }

    public string? SubjectEmail { get; init; }

    public Guid? SubjectUserId { get; init; }

    public string? ErrorCode { get; init; }

    public string? Detail { get; init; }
}
