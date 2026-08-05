using SIMF.Common.Enums;
using SIMF.Domain.Programme;
using SIMF.Domain.Exhibition;
using SIMF.Domain.Common;

namespace SIMF.Domain.Venue;

/// <summary>
/// One node on the 2D venue map: a position, plus an optional reference to the
/// hall or booth it marks. Logistics staff place the nodes and the app renders
/// them.
/// </summary>
public sealed class VenueMapNode : BaseAuditEntity
{
    public string Label { get; set; } = string.Empty;
    public string LabelArabic { get; set; } = string.Empty;

    public VenueMapNodeKind Kind { get; set; }

    /// <summary>Relative units, roughly 0 to 1000, which the app scales to its
    /// own canvas.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Set when this node marks a hall. Delete is restricted, so a hall
    /// cannot vanish from under its node.</summary>
    public Guid? HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Set when this node marks a booth, restricted in the same
    /// way.</summary>
    public Guid? BoothId { get; set; }
    public Booth? Booth { get; set; }
}
