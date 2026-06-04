using SIMF.Domain.Programme;

namespace SIMF.Domain.SeatReservations;

/// <summary>D-175 (gap doc G11, Mockup page 7) — visual seat-grid
/// layout for a <see cref="Hall"/>. Optional 1:1 with Hall (a hall
/// without a layout has no per-seat picker and falls back to
/// random-only allocation against <see cref="Hall.Capacity"/>).
/// <para>RowLabels is a comma-separated list of row identifiers
/// ("A,B,C" or "VIP,A,B"); the seat count per row is uniform
/// (<see cref="SeatsPerRow"/>). The product
/// <c>RowLabels.Count * SeatsPerRow</c> must not exceed
/// <see cref="Hall.Capacity"/> — validated at the service layer.</para>
/// <para>Per the D-110 freeze on the Hall table, layout columns live
/// on this separate table rather than as extra columns on
/// <see cref="Hall"/>.</para></summary>
public sealed class HallSeatLayout
{
    public Guid Id { get; set; }

    /// <summary>FK to <see cref="Hall"/>. Unique — one layout per hall.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>CSV of row labels in display order, e.g. "A,B,C,D".
    /// 1–256 chars. Each individual label is 1–8 chars.</summary>
    public string RowLabels { get; set; } = string.Empty;

    /// <summary>Number of seats per row (1–80). Uniform across the grid.</summary>
    public int SeatsPerRow { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
