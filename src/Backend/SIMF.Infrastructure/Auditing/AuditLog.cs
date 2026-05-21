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
            EventType = entry.EventType,
            Outcome = entry.Outcome,
            SubjectEmail = entry.SubjectEmail,
            SubjectUserId = entry.SubjectUserId,
            SourceIp = requestContext.SourceIp,
            UserAgent = requestContext.UserAgent,
            CorrelationId = requestContext.CorrelationId,
            ErrorCode = entry.ErrorCode,
            Detail = entry.Detail,
        };

        dbContext.OperationLog.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Audit {EventType} {Outcome} for {SubjectEmail} from {SourceIp}",
            record.EventType,
            record.Outcome,
            record.SubjectEmail,
            record.SourceIp);
    }
}
