namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Operation log viewer — read-only over
/// the existing <c>OperationLogEntry</c> table.</summary>
public sealed record AdminOperationLogSummary(
    Guid Id,
    DateTime Timestamp,
    string EventType,
    string Outcome,
    string? SubjectEmail,
    Guid? ActorUserId,
    string? SourceIp,
    string? CorrelationId,
    string? ErrorCode);

/// <summary>The detail payload for `GET /admin/operation-log/{id}` — the
/// full row including UserAgent + Detail.</summary>
public sealed record AdminOperationLogDetail(
    Guid Id,
    DateTime Timestamp,
    string EventType,
    string Outcome,
    string? SubjectEmail,
    Guid? SubjectUserId,
    Guid? ActorUserId,
    string? SourceIp,
    string? UserAgent,
    string? CorrelationId,
    string? ErrorCode,
    string? Detail);
