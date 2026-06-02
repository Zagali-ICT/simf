namespace SIMF.Common.Enums;

/// <summary>P2.5 — D-230 (SIMF-FDS-006 §5.3/§7, FR-605): what a 2D venue-map
/// node marks. A node optionally references the Hall or Booth it represents;
/// Zone / PointOfInterest nodes are free-standing labels with a position.</summary>
public enum VenueMapNodeKind
{
    Hall = 0,
    Zone = 1,
    Booth = 2,
    PointOfInterest = 3,
}
