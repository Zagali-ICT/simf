using SIMF.Common.Enums;

namespace SIMF.Contracts.Programme;

/// <summary>One venue-map node as the app reads it. The
/// Flutter 2D map renders nodes at (X, Y) and links Hall/Booth nodes to their
/// detail pages. Append-only wire shape.</summary>
public sealed record PublicVenueMapNode(
    Guid Id,
    string Label,
    string LabelArabic,
    VenueMapNodeKind Kind,
    double X,
    double Y,
    Guid? HallId,
    Guid? BoothId);
