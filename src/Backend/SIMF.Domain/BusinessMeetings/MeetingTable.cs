using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// One bookable table inside a hall. Tables belong to a hall whose purpose is
/// Meeting or General, and can be added one at a time or generated in bulk. A
/// <see cref="BusinessMeeting"/> is scheduled at one table for a time slot.
/// </summary>
public sealed class MeetingTable
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Cascade-deleted with the hall.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>A short code such as "T-01", entered or generated, and unique
    /// among the active tables in its hall.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Set when the table was placed by the row-and-column mode, paired
    /// with <see cref="ColumnNumber"/>.</summary>
    public string? RowLabel { get; set; }

    /// <summary>One-based within the row.</summary>
    public int? ColumnNumber { get; set; }

    /// <summary>Seats at the table, which caps a meeting's participant
    /// count.</summary>
    public int Capacity { get; set; } = 2;

    /// <summary>Soft-delete flag, which the list and picker endpoints filter
    /// on.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Drops the table out of the pickers and the default grid.
    /// Idempotent.</summary>
    public void Deactivate() => IsActive = false;
}
