// Tests: SIMF.Api.Tests/AuditLogTests.cs (single write, batched write, and the
//        swallowed failure), SIMF.Api.Tests/AuditLogBatchFallbackTests.cs (the
//        interface's default per-entry fallback for stores that cannot batch).
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure.Persistence;
using SIMF.Common;

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
        var record = BuildRecord(entry);

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

        LogWritten(record);
    }

    /// <summary>The batched write: one INSERT set and one save for the whole
    /// collection. The pre-start no-show sweep frees many holds in one pass, and
    /// auditing them one at a time cost a round trip per seat.</summary>
    /// <remarks>The failure posture is the single-entry one, applied to the set:
    /// the batch is logged and swallowed, because an audit write must never break
    /// the operation it records. The consequence of batching is that the set
    /// succeeds or fails together — acceptable here, since every entry in a batch
    /// describes the same sweep, so a partial trail would be the harder one to
    /// read.</remarks>
    public async Task WriteManyAsync(
        IReadOnlyCollection<AuditEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            return;
        }

        var records = entries.Select(BuildRecord).ToList();

        try
        {
            dbContext.OperationLog.AddRange(records);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to write {Count} audit entries of type {EventType}; the operation itself was not affected.",
                records.Count,
                records[0].EventType);
            return;
        }

        // Still one structured-log line per event. The class contract is that every
        // audited event reaches BOTH the operation log and the application log, and
        // a single "wrote 12 entries" summary would quietly drop half of that.
        foreach (var record in records)
        {
            LogWritten(record);
        }
    }

    /// <summary>Builds the persisted row from the caller's entry plus the request
    /// context. Shared by both write paths so the clipping lengths and the actor
    /// display-name fallback cannot drift between them.</summary>
    private OperationLogEntry BuildRecord(AuditEntry entry) =>
        new()
        {
            Id = Guid.NewGuid(),
            Timestamp = timeProvider.SimfNow(),
            EventType = Clip(entry.EventType, 80) ?? string.Empty,
            Outcome = entry.Outcome,
            SubjectEmail = Clip(entry.SubjectEmail, 256),
            SubjectUserId = entry.SubjectUserId,
            // Snapshot the subject name from the caller; null is
            // acceptable when the caller doesn't have the name on hand.
            SubjectDisplayName = Clip(entry.SubjectDisplayName, 128),
            ActorUserId = entry.ActorUserId,
            // Actor snapshot: prefer the caller's explicit value,
            // fall back to the JWT display_name claim for the typical
            // "actor performed this themselves" case.
            ActorDisplayName = Clip(entry.ActorDisplayName ?? requestContext.ActorDisplayName, 128),
            SourceIp = Clip(requestContext.SourceIp, 64),
            UserAgent = Clip(requestContext.UserAgent, 512),
            CorrelationId = Clip(requestContext.CorrelationId, 64),
            ErrorCode = Clip(entry.ErrorCode, 64),
            Detail = Clip(entry.Detail, 1024),
        };

    private void LogWritten(OperationLogEntry record) =>
        logger.LogInformation(
            "Audit {EventType} {Outcome} for {SubjectEmail} from {SourceIp}",
            record.EventType,
            record.Outcome,
            record.SubjectEmail,
            record.SourceIp);

    /// <summary>Trims a value to a maximum length so it cannot overflow its column.</summary>
    private static string? Clip(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];
}
