using SIMF.Common.Enums;
using SIMF.Domain.Profiles;

namespace SIMF.Domain.AccessControl;

/// <summary>
/// One recorded gate scan. Append-only by construction: nothing in the codebase
/// updates or deletes a scan row, and the entity opts out of the row-audit
/// interceptor because it is itself an audit log. The append-only rule is a code
/// convention, not a database guarantee -- there is no trigger behind it.
/// </summary>
public class GateScan
{
    /// <summary>A clustered bigint identity, so inserts stay monotonic and the
    /// index does not fragment under scan load.</summary>
    public long Id { get; set; }

    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>Null when the QR resolved to nothing, which the denial code
    /// records as an unknown QR.</summary>
    public Guid? UserProfileId { get; set; }
    public UserProfile? UserProfile { get; set; }

    /// <summary>Whatever the scanner physically presented, preserved verbatim
    /// even after a QR rotation so the scan stays forensically traceable. Not a
    /// bare 12-character serial: the event badge carries an encrypted payload of
    /// roughly 54 characters, which is why the column is nvarchar(96) and the
    /// service truncates anything longer rather than failing the write.</summary>
    public string QrIdAtScan { get; set; } = string.Empty;

    /// <summary>The visitor's display name as it stood at the moment of the
    /// scan. Snapshotting it lets the log still render a name after the linked
    /// Identity row is deleted or renamed. Null when the QR did not
    /// resolve.</summary>
    public string? ScannedDisplayName { get; set; }

    /// <summary>The visitor's profile-type name at the moment of the scan, held
    /// for the same reason as <see cref="ScannedDisplayName"/>.</summary>
    public string? ScannedProfileTypeName { get; set; }

    /// <summary>At a gate configured for both directions the engine infers this
    /// from the visitor's last allowed scan there.</summary>
    public ScanDirection Direction { get; set; }

    public ScanOutcome Outcome { get; set; }

    /// <summary>Set on a denied scan, null on success.</summary>
    public DenialReasonCode? DenialReasonCode { get; set; }

    public ScanSource Source { get; set; }

    /// <summary>Ties the row back to the API correlation id for the request.</summary>
    public string? CorrelationId { get; set; }

    public string? UserAgent { get; set; }

    /// <summary>Kept for abuse investigation.</summary>
    public string? IpAddress { get; set; }

    /// <summary>The client's idempotency key, when it sent one. A replay returns
    /// the outcome recorded here rather than scanning again.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>The operator, kiosk or system principal that performed the scan.
    /// A bare Guid: the user lives in the Identity database.</summary>
    public Guid ScannedByUserId { get; set; }

    /// <summary>Server clock at receive, and the authoritative timestamp.</summary>
    public DateTime ScannedAt { get; set; }

    /// <summary>The device's own local scan time, always treated as asserted by
    /// the client and never authoritative. Null when none was supplied.</summary>
    public DateTime? ClientScannedAt { get; set; }
}
