using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Auditing;

/// <summary>
/// Writes audit entries to the operation-log table in the application database,
/// enriching each with the request context (source IP, user-agent, correlation
/// id). The same event is also written to the structured application log.
/// </summary>
/// <remarks>
/// A failed audit write is logged and swallowed — the audit log must never
/// break the operation it is recording. Request-context fields are clipped to
/// their column lengths so an oversized untrusted header cannot fail the write.
/// </remarks>
internal sealed class AuditLog(
    SimfAppDbContext dbContext,
    IRequestContext requestContext,
    TimeProvider timeProvider,
    ILogger<AuditLog> logger) : IAuditLog
{
    public async Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        var record = new OperationLogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = timeProvider.GetUtcNow(),
            EventType = Clip(entry.EventType, 80) ?? string.Empty,
            Outcome = entry.Outcome,
            SubjectEmail = Clip(entry.SubjectEmail, 256),
            SubjectUserId = entry.SubjectUserId,
            ActorUserId = entry.ActorUserId,
            SourceIp = Clip(requestContext.SourceIp, 64),
            UserAgent = Clip(requestContext.UserAgent, 512),
            CorrelationId = Clip(requestContext.CorrelationId, 64),
            ErrorCode = Clip(entry.ErrorCode, 64),
            Detail = Clip(entry.Detail, 1024),
        };

        try
        {
            dbContext.OperationLog.Add(record);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to write the audit entry {EventType}; the operation itself was not affected.",
                record.EventType);
            return;
        }

        logger.LogInformation(
            "Audit {EventType} {Outcome} for {SubjectEmail} from {SourceIp}",
            record.EventType,
            record.Outcome,
            record.SubjectEmail,
            record.SourceIp);
    }

    /// <summary>Trims a value to a maximum length so it cannot overflow its column.</summary>
    private static string? Clip(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
