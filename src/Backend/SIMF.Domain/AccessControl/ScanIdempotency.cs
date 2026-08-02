namespace SIMF.Domain.AccessControl;

/// <summary>
/// D-148 — the idempotency replay store for <c>POST /api/v1/gates/{id}/scans</c>
/// (SIMF-API-GATES-001 §9). Same <see cref="Key"/> + same payload → byte-identical
/// replay of the prior response, with response header <c>X-Idempotent-Replay: true</c>.
/// Same key with different payload → 409 IDEMPOTENCY_KEY_CONFLICT. Records expire
/// 24 hours after <see cref="StoredAt"/>.
/// </summary>
public class ScanIdempotency
{
    /// <summary>Client-generated UUIDv4 (36 chars).</summary>
    public string Key { get; set; } = string.Empty;

    public Guid GateId { get; set; }

    /// <summary>SHA-256 of the request body (qr + idempotencyKey + source).
    /// Mismatch with a prior request under the same key = conflict.</summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>SHA-256 of the recorded response body for replay verification.</summary>
    public string ResponseHash { get; set; } = string.Empty;

    /// <summary>The recorded GateScan row id, when one was created (denials
    /// and allowed scans both populate this).</summary>
    public long? ScanId { get; set; }

    public DateTime StoredAt { get; set; }
}
