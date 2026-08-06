namespace SIMF.Common.Enums;

/// <summary>D-174 (gap doc G11, Mockup page 27) — lifecycle state of
/// a <c>MeetingRequest</c>.</summary>
public enum MeetingRequestStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,

    /// <summary>D-500 (Wave 5, الطلبات 1408:9726) — the requester cancelled
    /// their own still-pending request from the app. Additive value
    /// (append-only, the frozen-enum rule); stored as an int, so the existing
    /// rows and the wire contract are untouched.</summary>
    Cancelled = 3,

    /// <summary>D-716 (item 7, FDS-013 §15 GAP-2) — a speaker meeting request the
    /// admin accepted AND bound to a hall slot: it now awaits the speaker's own
    /// confirmation (the double-opt-in advanced by Slice C's email links) before
    /// it becomes <see cref="Accepted"/>. Additive value (append-only, the
    /// frozen-enum rule), stored as an int. Admin-only state: the app-facing "My
    /// requests" feed folds it back to <see cref="Pending"/> so the shipped mobile
    /// wire contract (values 0–3) is preserved.</summary>
    AwaitingSpeaker = 4,

    /// <summary>Bi-Meeting rework — a confirmed meeting (speaker or delegation) whose
    /// parties were checked in at the assigned hall by an operator: the meeting took
    /// place. Terminal state. Additive value (append-only, the frozen-enum rule),
    /// stored as an int. Admin/operator-only state: the app-facing "My requests" feed
    /// folds it back to <see cref="Accepted"/> so the shipped mobile wire contract
    /// (values 0–3) is preserved. A Done meeting still HOLDS its (now past) slot —
    /// see <see cref="MeetingRequestStatuses.SlotHolding"/>.</summary>
    Done = 5,
}

/// <summary>The single authority for which
/// <see cref="MeetingRequestStatus"/> values HOLD a booked slot (hall or speaker):
/// <see cref="MeetingRequestStatus.Accepted"/> or
/// <see cref="MeetingRequestStatus.AwaitingSpeaker"/> or
/// <see cref="MeetingRequestStatus.Done"/>. Every "is this slot taken?"
/// consumer — the hall free-slot taken-filter, the speaker/hall double-booking
/// overlap re-checks, and the DB filtered-unique indexes — MUST agree with this set.
/// If a new slot-holding <see cref="MeetingRequestStatus"/> is ever appended, update
/// this array AND the DB index filters (which, since SQL Server filtered indexes
/// forbid <c>OR</c>, are expressed as "not a released state": <c>&lt;&gt; Pending
/// &lt;&gt; Rejected &lt;&gt; Cancelled</c>) together. Note <see cref="MeetingRequestStatus.Done"/>
/// is deliberately inside this set (a completed meeting keeps its past slot) and is
/// therefore already covered by the "not a released state" index filters — do not
/// "simplify" the filters to exclude it.</summary>
public static class MeetingRequestStatuses
{
    public static readonly MeetingRequestStatus[] SlotHolding =
    {
        MeetingRequestStatus.Accepted,
        MeetingRequestStatus.AwaitingSpeaker,
        MeetingRequestStatus.Done,
    };
}
