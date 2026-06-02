using SIMF.Common.Enums;
using SIMF.Domain.Programme;
using SIMF.Domain.Exhibition;

namespace SIMF.Domain.Venue;

/// <summary>P2.5 — D-230 (SIMF-FDS-006 §5.3/§7, FR-605): one node on the 2D
/// venue map — a position plus an optional reference to the Hall or Booth it
/// marks (the agreed 2D target, D-199). The Logistics role places nodes; the
/// app renders them. Soft-deleted via <see cref="IsActive"/>.</summary>
public sealed class VenueMapNode
{
    public Guid Id { get; set; }

    public string Label { get; set; } = string.Empty;
    public string LabelArabic { get; set; } = string.Empty;

    public VenueMapNodeKind Kind { get; set; }

    /// <summary>2D map coordinates (relative units, e.g. 0–1000). The app
    /// scales them to its canvas.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>Optional real FK to the <see cref="Hall"/> this node marks
    /// (Kind = Hall). Restrict delete.</summary>
    public Guid? HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Optional real FK to the <see cref="Booth"/> this node marks
    /// (Kind = Booth). Restrict delete.</summary>
    public Guid? BoothId { get; set; }
    public Booth? Booth { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    public void Deactivate() => IsActive = false;
}
