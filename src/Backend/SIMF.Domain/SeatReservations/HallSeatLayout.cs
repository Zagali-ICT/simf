// Tests: SIMF.Api.Tests/SeatReservationsTests.cs
using SIMF.Domain.Programme;

namespace SIMF.Domain.SeatReservations;

/// <summary>The seat grid for a <see cref="Hall"/>; without one the hall has no
/// per-seat picker and allocates randomly against <see cref="Hall.Capacity"/>. The
/// CSV columns are parallel, one entry per row, validated at the service layer.</summary>
public sealed class HallSeatLayout
{
    public Guid Id { get; set; }

    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Row labels in display order, comma-separated: "A,B,C" or "VIP,A,B".</summary>
    public string RowLabels { get; set; } = string.Empty;

    /// <summary>The uniform row width while <see cref="SeatCounts"/> is null; on a
    /// ragged layout it holds the largest per-row count.</summary>
    public int SeatsPerRow { get; set; }

    /// <summary>Per-row seat counts, making the grid ragged; null keeps every row at
    /// <see cref="SeatsPerRow"/>. Their sum may not exceed <see cref="Hall.Capacity"/>.</summary>
    public string? SeatCounts { get; set; }

    /// <summary>Per-row <see cref="SIMF.Common.Enums.SeatTier"/>; null is all-Normal.</summary>
    public string? SeatTiers { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
