using SIMF.Common.Enums;
using SIMF.Domain.Profiles;

namespace SIMF.Domain.AccessControl;

/// <summary>
/// D-148 — one recorded gate scan (SIMF-FDS-003 §5.6 / design notes §3.1).
/// Append-only: an INSTEAD-OF UPDATE/DELETE trigger refuses mutation, and
/// the entity opts out of the <c>RowAudit</c> interceptor because it is
/// itself an audit log. The PK is a <c>bigint IDENTITY</c> (clustered) for
/// monotonic inserts under high load.
/// </summary>
public class GateScan
{
    /// <summary>Monotonic bigint PK — clustered, identity, no fragmentation
    /// under high insert load.</summary>
    public long Id { get; set; }




    public Guid GateId { get; set; }
    public Gate? Gate { get; set; }

    /// <summary>The resolved <c>UserProfile.Id</c>. Null when the QR
    /// resolved to nothing (DenialReasonCode = QR_UNKNOWN). Logical FK to
    /// <c>UserProfile</c> (cross-DB — D-157).</summary>
    public Guid? UserProfileId { get; set; }
    public UserProfile? UserProfile  { get; set; }

   
    /// <summary>The 12-character QR exactly as scanned. Preserved verbatim
    /// even after QR rotation so the scan stays forensically traceable.</summary>
    public string QrIdAtScan { get; set; } = string.Empty;
    /// <summary>D-157 — snapshot of the scanned visitor's display name
    /// at the moment of the scan. Lets the scan log render "Ahmad Salem"
    /// even after the linked Identity-DB row is deleted, renamed, or
    /// otherwise diverges. Null when <see cref="UserProfileId"/> is null
    /// (QR didn't resolve).</summary>
    public string? ScannedDisplayName { get; set; }

    /// <summary>D-157 — snapshot of the scanned visitor's profile-type
    /// name (e.g. "VVIP", "Staff") at the moment of the scan. Same
    /// rationale as <see cref="ScannedDisplayName"/>.</summary>
    public string? ScannedProfileTypeName { get; set; }



   
    /// <summary>Resolved direction. For Both-mode gates the engine infers
    /// this in step 10 from the visitor's last allowed scan at this gate.</summary>
    public ScanDirection Direction { get; set; }

    /// <summary>The scan outcome.</summary>
    public ScanOutcome Outcome { get; set; }

    /// <summary>The denial code on a denied scan; null on success.</summary>
    public DenialReasonCode? DenialReasonCode { get; set; }


    
   
    /// <summary>Where the scan came from.</summary>
    public ScanSource Source { get; set; }

    /// <summary>Tie-back to the API correlation id for the request.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Caller user-agent.</summary>
    public string? UserAgent { get; set; }

    /// <summary>Caller IP — for abuse investigation.</summary>
    public string? IpAddress { get; set; }



    /// <summary>The UUIDv4 idempotency key the client sent, when present.
    /// Replays return the original recorded outcome (design notes §5).</summary>
    public string? IdempotencyKey { get; set; }


   

    /// <summary>The operator (or system / kiosk principal) who performed
    /// the scan. Logical FK to <c>SimfUser</c>.</summary>
    public Guid ScannedByUserId { get; set; }
    /// <summary>Server clock at receive. The authoritative timestamp.</summary>
    public DateTimeOffset ScannedAtUtc { get; set; }

    /// <summary>Device-asserted local scan time. Always treated as
    /// client-asserted, never authoritative. Null when the client did
    /// not supply it.</summary>
    public DateTimeOffset? ClientScannedAtUtc { get; set; }


}
