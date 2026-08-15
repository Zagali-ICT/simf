using SIMF.Common.Enums;
using SIMF.Domain.Profiles;

namespace SIMF.Domain.AccessControl;

/// <summary>One recorded gate scan. Append-only by code convention, not by any database guarantee: nothing in the codebase updates or deletes a row.</summary>
public class GateScan
{
    public long Id { get; set; }

    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>Null when the QR resolved to nothing, which the denial code records as an unknown QR.</summary>
    public Guid? UserProfileId { get; set; }
    public UserProfile? UserProfile { get; set; }

    /// <summary>What the scanner physically presented, kept verbatim through a QR rotation so the scan stays forensically traceable.</summary>
    public string QrIdAtScan { get; set; } = string.Empty;

    /// <summary>Snapshotted at scan time so the log still renders a name after the linked Identity row is deleted or renamed.</summary>
    public string? ScannedDisplayName { get; set; }

    public string? ScannedProfileTypeName { get; set; }

    public ScanDirection Direction { get; set; }

    public ScanOutcome Outcome { get; set; }

    public DenialReasonCode? DenialReasonCode { get; set; }

    public ScanSource Source { get; set; }

    public string? CorrelationId { get; set; }

    public string? UserAgent { get; set; }

    public string? IpAddress { get; set; }

    /// <summary>A replay under the same key returns the outcome recorded here rather than scanning again.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>The scanning operator, kiosk or system principal. Bare Guid: the user lives in the Identity database.</summary>
    public Guid ScannedByUserId { get; set; }

    /// <summary>Server clock at receive, and the authoritative one; <see cref="ClientScannedAt"/> is only asserted by the device.</summary>
    public DateTime ScannedAt { get; set; }

    public DateTime? ClientScannedAt { get; set; }
}
