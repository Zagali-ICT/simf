using SIMF.Common.Enums;

namespace SIMF.Contracts.Admin;

/// <summary>One venue-map node in the admin list.</summary>
public sealed record AdminVenueMapNodeSummary(
    Guid Id,
    string Label,
    string LabelArabic,
    VenueMapNodeKind Kind,
    double X,
    double Y,
    Guid? HallId,
    Guid? BoothId,
    bool IsActive);

/// <summary>P2.5 — full venue-map node detail.</summary>
public sealed record AdminVenueMapNodeDetail(
    Guid Id,
    string Label,
    string LabelArabic,
    VenueMapNodeKind Kind,
    double X,
    double Y,
    Guid? HallId,
    Guid? BoothId,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>P2.5 — create a venue-map node.</summary>
public sealed class AdminCreateVenueMapNodeRequest
{
    public string Label { get; set; } = string.Empty;
    public string LabelArabic { get; set; } = string.Empty;
    public VenueMapNodeKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public Guid? HallId { get; set; }
    public Guid? BoothId { get; set; }
}

/// <summary>P2.5 — update a venue-map node (move / relabel / re-link).</summary>
public sealed class AdminUpdateVenueMapNodeRequest
{
    public string Label { get; set; } = string.Empty;
    public string LabelArabic { get; set; } = string.Empty;
    public VenueMapNodeKind Kind { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public Guid? HallId { get; set; }
    public Guid? BoothId { get; set; }
    public bool IsActive { get; set; } = true;
}
