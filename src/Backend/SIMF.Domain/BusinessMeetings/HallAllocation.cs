using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — a reservation of a hall (or part of it) for a
/// <see cref="HallPurpose"/> over a from–to time-slot. This is the flexible
/// allocation layer the owner asked for: "reservation can be whole, rand by count,
/// by row column pre-reserved … a hall can be reserved for a session or for a
/// meeting." A meeting hall's tables are reserved through these rows; the unified
/// layer is designed so session and booth halls can be expressed the same way (see
/// SIMF-FDS-013 §2 / OI-9).
///
/// <para>Released allocations stay in the table for audit; <see cref="ReleasedAt"/>
/// is set and the slot is freed for re-allocation.</para>
/// </summary>
public sealed class HallAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Real FK to <see cref="Hall.Id"/> on the App DB (cascade delete).</summary>
    public Guid HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>What the hall space is reserved for in this slot.</summary>
    public HallPurpose Purpose { get; set; }

    /// <summary>How the space is reserved (whole / random-by-count / row-column).</summary>
    public HallAllocationMode Mode { get; set; }

    /// <summary>For <see cref="HallAllocationMode.RandomByCount"/> — the number of
    /// units to allocate (capped at hall capacity). Null for the other modes.</summary>
    public int? UnitCount { get; set; }

    /// <summary>For <see cref="HallAllocationMode.RowColumn"/> — the CSV of named
    /// row/column units to reserve (e.g. "A1,A2,B3"). Null for the other modes.</summary>
    public string? RowColumnSpec { get; set; }

    /// <summary>Slot start (inclusive, UTC).</summary>
    public DateTimeOffset StartUtc { get; set; }

    /// <summary>Slot end (exclusive, UTC). Must be after <see cref="StartUtc"/>.</summary>
    public DateTimeOffset EndUtc { get; set; }

    /// <summary>The admin who created the allocation — logical FK to
    /// <c>SimfUser.Id</c> on the Identity DB (bare Guid, no navigation).</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>Optional free-text note (≤ 512 chars).</summary>
    public string? Notes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set when the allocation is released; released rows are excluded
    /// from overlap checks so the slot is re-allocatable.</summary>
    public DateTimeOffset? ReleasedAt { get; set; }
}
