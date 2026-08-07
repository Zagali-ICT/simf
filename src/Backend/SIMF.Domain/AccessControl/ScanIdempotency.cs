namespace SIMF.Domain.AccessControl;

/// <summary>
/// The replay store behind the gate-scan endpoint's idempotency key. The same
/// key with the same payload replays the earlier response byte for byte, flagged
/// as a replay in a response header; the same key with a different payload is a
/// conflict. Records expire a day after they are stored.
/// </summary>
public class ScanIdempotency
{
    /// <summary>A client-generated UUID.</summary>
    public string Key { get; set; } = string.Empty;

    public Guid GateId { get; set; }

    /// <summary>A hash of the request body. A mismatch against an earlier request
    /// under the same key is what makes the pair a conflict.</summary>
    public string RequestHash { get; set; } = string.Empty;

    /// <summary>A hash of the recorded response, for verifying a replay.</summary>
    public string ResponseHash { get; set; } = string.Empty;

    /// <summary>The scan row that was written, if one was. Both allowed and
    /// denied scans record one.</summary>
    public long? ScanId { get; set; }

    public DateTime StoredAt { get; set; }
}
