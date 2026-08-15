using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>A reservation of a hall, or part of one, for a purpose over a time slot.</summary>
public sealed class HallAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    public HallPurpose Purpose { get; set; }

    public HallAllocationMode Mode { get; set; }

    /// <summary>Set only in the random-by-count mode.</summary>
    public int? UnitCount { get; set; }

    /// <summary>Named units such as "A1,A2,B3". Set only in the row-and-column mode.</summary>
    public string? RowColumnSpec { get; set; }

    /// <summary>Inclusive; <see cref="End"/> is exclusive. Saudi local time.</summary>
    public DateTime Start { get; set; }

    public DateTime End { get; set; }

    /// <summary>A bare Guid: the user lives in the Identity database.</summary>
    public Guid CreatedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set on release. Released rows are excluded from the overlap check, which frees the slot.</summary>
    public DateTime? ReleasedAt { get; set; }
}
