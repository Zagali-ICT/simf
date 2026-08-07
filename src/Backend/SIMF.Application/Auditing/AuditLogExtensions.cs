using SIMF.Common.Enums;

namespace SIMF.Application.Auditing;

/// <summary>
/// Shorthand for the two audit shapes that dominate the codebase: an action
/// succeeded, or an action was refused. Both carry only the event type, who did
/// it, and a free-text detail.
///
/// <para>Entries that also name a subject — an admin acting on someone else's
/// account — still build <see cref="AuditEntry"/> directly, because spelling
/// out the subject is the point of those records.</para>
/// </summary>
public static class AuditLogExtensions
{
    public static Task WriteSuccessAsync(
        this IAuditLog auditLog,
        string eventType,
        Guid? actorUserId,
        string? detail = null,
        CancellationToken cancellationToken = default) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = eventType,
                Outcome = AuditOutcome.Success,
                ActorUserId = actorUserId,
                Detail = detail,
            },
            cancellationToken);

    public static Task WriteFailureAsync(
        this IAuditLog auditLog,
        string eventType,
        Guid? actorUserId,
        string? errorCode = null,
        string? detail = null,
        CancellationToken cancellationToken = default) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = eventType,
                Outcome = AuditOutcome.Failure,
                ActorUserId = actorUserId,
                ErrorCode = errorCode,
                Detail = detail,
            },
            cancellationToken);
}
