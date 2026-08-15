namespace SIMF.Domain.AccessControl;

/// <summary>Replay store for the gate-scan idempotency key: the same key with the same payload replays the earlier response, with a different payload it is a conflict.</summary>
public class ScanIdempotency
{
    public string Key { get; set; } = string.Empty;

    public Guid GateId { get; set; }

    public string RequestHash { get; set; } = string.Empty;

    public string ResponseHash { get; set; } = string.Empty;

    /// <summary>The scan row written, where one was; both allowed and denied scans write one.</summary>
    public long? ScanId { get; set; }

    public DateTime StoredAt { get; set; }
}
