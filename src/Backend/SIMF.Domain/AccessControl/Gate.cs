using SIMF.Common.Enums;
using SIMF.Domain.Common;

namespace SIMF.Domain.AccessControl;

/// <summary>A venue access point. Every scan is checked against its direction mode, its profile-type allow-list and its operator assignments.</summary>
public class Gate : BaseAuditEntity
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? DescriptionArabic { get; set; }

    /// <summary>Both makes the server infer direction from the visitor's last allowed scan at this gate.</summary>
    public DirectionMode DirectionMode { get; set; } = DirectionMode.Both;

    /// <summary>Set on a hall-door gate, where an allowed scan opens or closes the attendee's hall attendance; null makes it a perimeter gate.</summary>
    public Guid? HallId { get; set; }

    /// <summary>Empty allows every profile type; non-empty filters against the active ones, and deactivating them all denies every scan rather than reverting to allow-all.</summary>
    public ICollection<GateProfileTypeAllow> AllowedProfileTypes { get; set; } = [];

    public ICollection<GateAssignment> Assignments { get; set; } = [];
}
