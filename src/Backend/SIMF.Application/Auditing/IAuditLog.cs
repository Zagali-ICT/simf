namespace SIMF.Application.Auditing;

/// <summary>Writes an entry to the durable operation log — the audit trail.</summary>
public interface IAuditLog
{
    Task WriteAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Writes a whole set of entries that one operation produced at once.
    /// The pre-start no-show sweep audits every seat it frees, so a per-entry write
    /// costs a database round trip per released seat; a store that can persist the
    /// set in a single save overrides this to do exactly that.
    ///
    /// <para>The default body writes them one at a time through
    /// <see cref="WriteAsync"/>, which is the correct behaviour for any
    /// implementation that has nothing to batch — it keeps per-entry semantics and
    /// keeps the existing implementors compiling, rather than forcing every one of
    /// them to hand-roll the same loop.</para></summary>
    async Task WriteManyAsync(
        IReadOnlyCollection<AuditEntry> entries,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        foreach (var entry in entries)
        {
            await WriteAsync(entry, cancellationToken);
        }
    }
}
