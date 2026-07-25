using SIMF.Common.Enums;

namespace SIMF.Contracts.BusinessMeetings;

// SIMF-FDS-013 (owner, 2026-06-03) — Control Panel contracts for the flexible
// hall configuration + admin-arranged B2B/B2C business-meeting module. All
// admin-only. Visitor party refs are bare Guids to the Identity DB (resolved on
// read); company party refs are App FKs.

// ── Hall purpose ─────────────────────────────────────────────────────────────

/// <summary>Set a hall's <see cref="HallPurpose"/> (booth / session / meeting /
/// general). Route-bound, so it carries no id.</summary>
public class SetHallPurposeRequest
{
    public HallPurpose Purpose { get; set; }
}

// ── Meeting tables ───────────────────────────────────────────────────────────

/// <summary>One row in the meeting-tables grid for a hall.</summary>
public sealed record MeetingTableRow(
    Guid Id,
    Guid HallId,
    string Code,
    string? RowLabel,
    int? ColumnNumber,
    int Capacity,
    bool IsActive);

/// <summary>Create a single meeting table in a hall.</summary>
public class CreateMeetingTableRequest
{
    public string Code { get; set; } = string.Empty;
    public string? RowLabel { get; set; }
    public int? ColumnNumber { get; set; }
    public int Capacity { get; set; } = 2;
}

/// <summary>Update a meeting table (route-bound id).</summary>
public class UpdateMeetingTableRequest
{
    public string Code { get; set; } = string.Empty;
    public string? RowLabel { get; set; }
    public int? ColumnNumber { get; set; }
    public int Capacity { get; set; } = 2;
}

/// <summary>Bulk-generate meeting tables for a hall — random-by-count or
/// by-row-column (the owner's "add randomly or by row/column"). Whole is not a
/// generation mode (it reserves the hall, not tables) and is rejected here.</summary>
public class GenerateMeetingTablesRequest
{
    public HallAllocationMode Mode { get; set; }

    /// <summary>For RandomByCount — how many tables to create. Bounded by the
    /// hall's free table slots (its capacity, hard-capped at 500); fewer may be
    /// created if the hall is near capacity.</summary>
    public int? Count { get; set; }

    /// <summary>For RowColumn — CSV of row/column codes, e.g. "A1,A2,B3".</summary>
    public string? RowColumnSpec { get; set; }

    /// <summary>Seats per generated table (≥ 2).</summary>
    public int Capacity { get; set; } = 2;

    /// <summary>When true, deactivate the hall's existing tables first
    /// (the owner's "reset random").</summary>
    public bool Reset { get; set; }
}

/// <summary>Result of a bulk generate.</summary>
public sealed record MeetingTablesGenerated(int Created, int Removed);

// ── Hall allocations ─────────────────────────────────────────────────────────

/// <summary>One row in the hall-allocations grid.</summary>
public sealed record HallAllocationRow(
    Guid Id,
    Guid HallId,
    HallPurpose Purpose,
    HallAllocationMode Mode,
    int? UnitCount,
    string? RowColumnSpec,
    DateTimeOffset Start,
    DateTimeOffset End,
    string? Notes);

/// <summary>Reserve hall space (whole / random-by-count / row-column) for a
/// purpose over a from–to time-slot.</summary>
public class CreateHallAllocationRequest
{
    public HallPurpose Purpose { get; set; }
    public HallAllocationMode Mode { get; set; }
    public int? UnitCount { get; set; }
    public string? RowColumnSpec { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Notes { get; set; }
}

// ── Business meetings ────────────────────────────────────────────────────────

/// <summary>One participant on the schedule request.</summary>
public sealed class ScheduleMeetingParticipant
{
    public MeetingPartyKind Kind { get; set; }

    /// <summary>App FK to Company when <see cref="Kind"/> = Company.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>Bare Identity user id when <see cref="Kind"/> = Visitor.</summary>
    public Guid? VisitorUserId { get; set; }
}

/// <summary>Schedule an admin-arranged meeting at a table for a time-slot.</summary>
public sealed class ScheduleMeetingRequest
{
    public Guid MeetingTableId { get; set; }
    public BusinessMeetingType MeetingType { get; set; }
    public DateTimeOffset Start { get; set; }
    public DateTimeOffset End { get; set; }
    public string? Notes { get; set; }
    public List<ScheduleMeetingParticipant> Participants { get; set; } = [];
}

/// <summary>Cancel a confirmed meeting (route-bound id). Reason optional (OI-6).</summary>
public class CancelMeetingRequest
{
    public string? Reason { get; set; }
}

/// <summary>One resolved participant for read responses.</summary>
public sealed record MeetingParticipantDto(
    MeetingPartyKind Kind,
    Guid? CompanyId,
    Guid? VisitorUserId,
    string DisplayName);

/// <summary>One row in the meetings grid.</summary>
public sealed record BusinessMeetingRow(
    Guid Id,
    Guid MeetingTableId,
    string TableCode,
    Guid HallId,
    string HallName,
    BusinessMeetingType MeetingType,
    DateTimeOffset Start,
    DateTimeOffset End,
    BusinessMeetingStatus Status,
    int ParticipantCount);

/// <summary>Full meeting detail (with participants).</summary>
public sealed record BusinessMeetingDetail(
    Guid Id,
    Guid MeetingTableId,
    string TableCode,
    Guid HallId,
    string HallName,
    BusinessMeetingType MeetingType,
    DateTimeOffset Start,
    DateTimeOffset End,
    BusinessMeetingStatus Status,
    string? Notes,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt,
    IReadOnlyList<MeetingParticipantDto> Participants);

/// <summary>Response after a successful schedule.</summary>
public sealed record BusinessMeetingScheduled(
    Guid Id,
    BusinessMeetingStatus Status,
    DateTimeOffset Start,
    DateTimeOffset End);
