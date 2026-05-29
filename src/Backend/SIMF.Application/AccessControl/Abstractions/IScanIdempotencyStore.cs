namespace SIMF.Application.AccessControl.Abstractions;

/// <summary>D-148 — 24h idempotency replay store (SIMF-API-GATES-001 §9).</summary>
public interface IScanIdempotencyStore
{
    /// <summary>Look up a prior request under the given key + gate. Returns
    /// <c>null</c> when there is no prior record. Records older than 24
    /// hours are treated as absent.</summary>
    Task<ScanIdempotencyRecord?> TryGetAsync(
        string key, Guid gateId, CancellationToken cancellationToken = default);

    /// <summary>Persist a fresh record. Should be called inside the same
    /// transaction as the matching <c>GateScan</c> row insertion.</summary>
    Task PersistAsync(
        string key, Guid gateId, string requestHash, string responseHash,
        long? scanId, CancellationToken cancellationToken = default);
}

/// <summary>D-148 — replay record (SIMF-API-GATES-001 §9).</summary>
public sealed record ScanIdempotencyRecord(
    string Key,
    Guid GateId,
    string RequestHash,
    string ResponseHash,
    long? ScanId,
    DateTimeOffset StoredAt);
