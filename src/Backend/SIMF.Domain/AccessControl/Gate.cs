using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.AccessControl;

/// <summary>
/// A configured venue access point. Each gate has a direction mode, an optional
/// allow-list of profile types and a set of assigned operators; the constraint
/// engine applies all three to every scan.
/// </summary>
public class Gate : BaseAuditEntity
{
    /// <summary>Unique regardless of case, and normalised to upper case by the
    /// service.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    /// <summary>In or Out gives the operator console a fixed direction button.
    /// Both makes the server infer direction from the visitor's last allowed scan
    /// at this gate.</summary>
    public DirectionMode DirectionMode { get; set; } = DirectionMode.Both;

    /// <summary>Set when this gate is the door of a hall, which makes an allowed
    /// check-in scan here also open the attendee's hall attendance for whichever
    /// session is live in that hall, and a check-out close it. Null makes it a
    /// perimeter gate that records the scan and nothing more. Nullable so every
    /// gate that already existed keeps perimeter behaviour. A real foreign key,
    /// since halls live in this database, but deliberately without a navigation
    /// property.</summary>
    public Guid? HallId { get; set; }

    /// <summary>Empty allows every profile type through. Non-empty filters
    /// against the active types, and deactivating every entry denies all scans
    /// rather than silently reverting to allowing everyone.</summary>
    public ICollection<GateProfileTypeAllow> AllowedProfileTypes { get; set; } = [];

    /// <summary>The engine checks active assignments only; inactive rows are
    /// kept for the audit trail.</summary>
    public ICollection<GateAssignment> Assignments { get; set; } = [];
}
