using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// A reservation of a hall, or part of one, for a purpose over a time slot. The
/// space can be taken whole, by a count of units picked at random, or by naming
/// rows and columns, which is what lets a meeting hall's tables and a session
/// hall's seats be expressed through the same layer.
///
/// <para>Released allocations stay in the table for the audit trail:
/// <see cref="ReleasedAt"/> is stamped and the slot becomes free again.</para>
/// </summary>
public sealed class HallAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>A real foreign key with delete restricted, so a hall holding
    /// allocations cannot be hard-deleted under them.</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    public HallPurpose Purpose { get; set; }

    public HallAllocationMode Mode { get; set; }

    /// <summary>How many units to allocate, capped at the hall's capacity. Set
    /// only in the random-by-count mode.</summary>
    public int? UnitCount { get; set; }

    /// <summary>The named units to reserve, such as "A1,A2,B3". Set only in the
    /// row-and-column mode.</summary>
    public string? RowColumnSpec { get; set; }

    /// <summary>Inclusive, Saudi local time.</summary>
    public DateTime Start { get; set; }

    /// <summary>Exclusive, Saudi local time, and must be after
    /// <see cref="Start"/>.</summary>
    public DateTime End { get; set; }

    /// <summary>The admin who created the allocation. A bare Guid: the user lives
    /// in the Identity database.</summary>
    public Guid CreatedByUserId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Set on release. Released rows are excluded from the overlap
    /// check, which is what frees the slot.</summary>
    public DateTime? ReleasedAt { get; set; }
}
