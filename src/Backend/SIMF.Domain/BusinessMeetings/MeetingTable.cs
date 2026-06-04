using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — one bookable meeting table inside a hall
/// (the "table/location in hall" the owner described). Tables live in a hall whose
/// <see cref="Hall.Purpose"/> is Meeting or General; they may be added one-by-one
/// or generated in bulk (random-by-count / by row-column). A
/// <see cref="BusinessMeeting"/> is scheduled at one table for a time-slot.
/// Soft-deleted via <see cref="IsActive"/>; admin grids/pickers filter on it.
/// </summary>
public sealed class MeetingTable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Real FK to <see cref="Hall.Id"/> on the App DB (cascade delete).</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Stable short code, unique among active tables in the hall
    /// (1–16 chars, e.g. "T-01"). Entered or generated.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Optional row label (1–8 chars) — set when the table is placed by
    /// the row/column allocation mode.</summary>
    public string? RowLabel { get; set; }

    /// <summary>Optional 1-based column number within the row — paired with
    /// <see cref="RowLabel"/> for the row/column allocation mode.</summary>
    public int? ColumnNumber { get; set; }

    /// <summary>Seats at the table (≥ 2). Caps a meeting's participant count
    /// (group meetings, OI-5).</summary>
    public int Capacity { get; set; } = 2;

    /// <summary>Soft-delete flag. List/pick endpoints filter on this.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Soft-delete: marks the table inactive so it drops out of pickers
    /// and the default grid. Idempotent.</summary>
    public void Deactivate() => IsActive = false;
}
