using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// D-478 (#11, Group G phase 2) — a delegation-to-delegation (G2G) meeting
/// request: a delegate (وفد) from one invited country asks to meet another
/// invited country's delegation, bringing <see cref="AttendeeCount"/> people, at a
/// proposed slot. The team reviews and Accepts/Rejects (same lifecycle enum as the
/// speaker meeting request, D-269); on accept the requester is notified + emailed.
/// Mirrors <see cref="SpeakerMeetingRequest"/> but keyed on countries, not a speaker.
/// </summary>
public sealed class DelegationMeetingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The delegate who submitted it. Logical FK to <c>SimfUser.Id</c>
    /// (Identity DB), resolved on read — no cross-DB relation (D-157).</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>The requester's own country (their nationality). Real FK to
    /// <see cref="Country"/> on the App DB.</summary>
    public int RequestingCountryId { get; set; }
    public Country? RequestingCountry { get; set; }

    /// <summary>The country whose delegation they want to meet. Real FK to
    /// <see cref="Country"/> on the App DB.</summary>
    public int TargetCountryId { get; set; }
    public Country? TargetCountry { get; set; }

    /// <summary>"count X" — how many of the requester's delegation will attend.</summary>
    public int AttendeeCount { get; set; }

    /// <summary>The meeting topic — free text up to 1000 chars.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The proposed slot (the team confirms it on accept). Optional.</summary>
    public DateTime? SlotStart { get; set; }
    public DateTime? SlotEnd { get; set; }

    /// <summary>Lifecycle state (unified machine — Bi-Meeting rework): Pending on
    /// create; AwaitingSpeaker = admin Approved + bound a hall slot, awaiting the other
    /// party's confirmation; Accepted = Confirmed (admin-verbal or other-party link/tap);
    /// Done = checked in at the hall; Rejected/Cancelled are releases.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>Bi-Meeting rework — the picked availability window the bound slot
    /// belongs to. Real FK to <see cref="DelegationAvailabilityWindow"/> (SetNull).
    /// Null for a requester-proposed slot or when the window is later removed.</summary>
    public Guid? AvailabilityWindowId { get; set; }

    /// <summary>Bi-Meeting rework — the hall the admin bound the meeting to on Approve.
    /// Real FK to <see cref="Hall"/> (SetNull). Null before approval; when set,
    /// <see cref="SlotStart"/>/<see cref="SlotEnd"/> hold the bound hall slot.</summary>
    public Guid? HallId { get; set; }
    public Hall? Hall { get; set; }

    /// <summary>Bi-Meeting rework — the optional meeting table inside
    /// <see cref="HallId"/>. Real FK to <see cref="MeetingTable"/> (SetNull).</summary>
    public Guid? MeetingTableId { get; set; }
    public MeetingTable? MeetingTable { get; set; }

    /// <summary>Optional admin response note / cancellation justification shown to the
    /// requester.</summary>
    public string? ResponseNote { get; set; }

    /// <summary>Bi-Meeting rework — when the meeting became Confirmed (admin-verbal or
    /// the other party's link/tap). Null until confirmed.</summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>Bi-Meeting rework — the actor who confirmed (admin id for a verbal
    /// confirm; null when confirmed by the other party's token/app-tap). Logical FK
    /// (Identity); no cross-DB relation (D-157).</summary>
    public Guid? ConfirmedByUserId { get; set; }

    /// <summary>Bi-Meeting rework — once-only dedup stamp for the 15-minute reminder
    /// worker (mirrors <c>Session.ReminderSent</c>). Null until the reminder fires.</summary>
    public DateTime? ReminderSent { get; set; }

    /// <summary>Bi-Meeting rework — when an operator checked the meeting in at the hall
    /// (flips it to <see cref="MeetingRequestStatus.Done"/>). Null until checked in.</summary>
    public DateTime? CheckedInAt { get; set; }

    /// <summary>Bi-Meeting rework — the operator who checked it in. Logical FK
    /// (Identity); no cross-DB relation (D-157).</summary>
    public Guid? CheckedInByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>The admin who responded. Logical FK (Identity); null while Pending.</summary>
    public Guid? RespondedByUserId { get; set; }
}
