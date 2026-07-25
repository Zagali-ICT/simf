// Tests: SIMF.Api.Tests/SeatReservationsTests.cs
using SIMF.Domain.Programme;

namespace SIMF.Domain.SeatReservations;

/// <summary>D-175 (gap doc G11, Mockup page 7) — visual seat-grid
/// layout for a <see cref="Hall"/>. Optional 1:1 with Hall (a hall
/// without a layout has no per-seat picker and falls back to
/// random-only allocation against <see cref="Hall.Capacity"/>).
/// <para>RowLabels is a comma-separated list of row identifiers
/// ("A,B,C" or "VIP,A,B"). By default the seat count per row is uniform
/// (<see cref="SeatsPerRow"/>). D-767 adds an optional per-row override
/// (<see cref="SeatCounts"/>): when it is set the grid becomes ragged —
/// each row has its own seat count — and <see cref="SeatsPerRow"/> is kept
/// only as the uniform fallback (storing <c>max(SeatCounts)</c>).
/// The layout capacity is <c>RowLabels.Count * SeatsPerRow</c> when uniform,
/// or <c>sum(SeatCounts)</c> when variable, and must not exceed
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

    /// <summary>Number of seats per row (1–80). The uniform fallback: it applies to
    /// every row when <see cref="SeatCounts"/> is null, and stores <c>max(SeatCounts)</c>
    /// when the layout is variable so an un-upgraded reader still sees a valid width.</summary>
    public int SeatsPerRow { get; set; }

    /// <summary>D-767 — optional per-row seat counts. Null = uniform (every row uses
    /// <see cref="SeatsPerRow"/>, unchanged pre-D-767 behaviour). When set it is a CSV
    /// of ints PARALLEL to <see cref="RowLabels"/> (e.g. "4,10,8,8"): its length equals
    /// <c>RowLabels.Count</c>, each count is 1–80, and <c>sum(SeatCounts) &lt;= Hall.Capacity</c>
    /// — all validated at the service layer. Max 256 chars.</summary>
    public string? SeatCounts { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
