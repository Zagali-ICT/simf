namespace SIMF.Common.Enums;

/// <summary>What a 2D venue-map
/// node marks. A node optionally references the Hall or Booth it represents;
/// Zone / PointOfInterest nodes are free-standing labels with a position.</summary>
public enum VenueMapNodeKind
{
    Hall = 0,
    Zone = 1,
    Booth = 2,
    PointOfInterest = 3,
}
