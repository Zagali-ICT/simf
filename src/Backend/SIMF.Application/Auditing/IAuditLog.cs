namespace SIMF.Application.Auditing;

/// <summary>Writes an entry to the durable operation log — the audit trail.</summary>
public interface IAuditLog
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);
}
