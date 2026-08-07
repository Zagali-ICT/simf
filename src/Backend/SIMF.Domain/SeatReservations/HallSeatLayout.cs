// Tests: SIMF.Api.Tests/SeatReservationsTests.cs
using SIMF.Domain.Programme;

namespace SIMF.Domain.SeatReservations;

/// <summary>
/// The visual seat grid for a <see cref="Hall"/>, optional and one per hall. A
/// hall without a layout has no per-seat picker and falls back to random-only
/// allocation against <see cref="Hall.Capacity"/>.
///
/// <para>The three CSV columns are parallel to one another: <see cref="RowLabels"/>
/// names the rows, and <see cref="SeatCounts"/> and <see cref="SeatTiers"/> — when
/// set — carry exactly one entry per row. Everything is validated at the service
/// layer, not by the database.</para>
///
/// <para>These columns live on their own table rather than on <see cref="Hall"/>
/// because the Hall table was frozen before seating shipped.</para>
/// </summary>
public sealed class HallSeatLayout
{
    public Guid Id { get; set; }

    /// <summary>Unique — one layout per hall.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Row labels in display order, comma-separated, such as "A,B,C,D"
    /// or "VIP,A,B". Up to 256 characters overall, each label 1 to 8.</summary>
    public string RowLabels { get; set; } = string.Empty;

    /// <summary>Seats per row, 1 to 80. Applies to every row while
    /// <see cref="SeatCounts"/> is null; when the layout is ragged this holds the
    /// largest per-row count instead, so a reader that predates ragged layouts
    /// still sees a usable grid width.</summary>
    public int SeatsPerRow { get; set; }

    /// <summary>Per-row seat counts, making the grid ragged. Null keeps every row
    /// at <see cref="SeatsPerRow"/>. Their sum may not exceed
    /// <see cref="Hall.Capacity"/>.</summary>
    public string? SeatCounts { get; set; }

    /// <summary>Per-row seat tiers, as <see cref="SIMF.Common.Enums.SeatTier"/>
    /// values. Null reads as an all-Normal grid, so a layout written before tiers
    /// existed loses no bookable seat. The Control Panel editor always writes this
    /// column and defaults a newly added row to VVIP.</summary>
    public string? SeatTiers { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
