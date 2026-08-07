using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// A delegation-to-delegation meeting request: a delegate from one country asks to meet
/// another invited country's delegation, bringing <see cref="AttendeeCount"/> people, at
/// a proposed slot the team then reviews. Mirrors <see cref="SpeakerMeetingRequest"/>,
/// but keyed on countries rather than a speaker.
///
/// <para>A requester holds one open request per target delegation: a repeat submission
/// moves the existing Pending row onto the new slot rather than adding a second, and only
/// the submit path enforces that.</para>
/// </summary>
public sealed class DelegationMeetingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>The delegate who submitted it. A bare Guid rather than a navigation: the
    /// user lives in the Identity database, so there is no foreign key across the two.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>Real foreign keys: the countries live in the same DbContext. The requesting
    /// country is the requester's nationality and need not be invited, so the host country
    /// can ask to meet the visiting ones; the target must be invited, and not the same.</summary>
    public int RequestingCountryId { get; set; }
    public Country? RequestingCountry { get; set; }

    public int TargetCountryId { get; set; }
    public Country? TargetCountry { get; set; }

    /// <summary>How many of the requester's delegation will attend; 1 to 100.</summary>
    public int AttendeeCount { get; set; }

    /// <summary>The meeting topic, up to 1000 characters.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Saudi local time, and null until proposed. The requester's proposed slot
    /// until an approval binds a hall slot over it, after which it is the meeting time of
    /// record. Set as a pair, End after Start, enforced by a database check constraint.</summary>
    public DateTime? SlotStart { get; set; }
    public DateTime? SlotEnd { get; set; }

    /// <summary>Pending on create; AwaitingSpeaker once the team has approved it and bound
    /// a hall slot, with the target delegation yet to confirm; Accepted once confirmed, by
    /// an admin recording a verbal confirmation or by the other party's emailed link or app
    /// tap; Done once an operator has checked it in at the hall. Rejected and Cancelled
    /// release the slot. Accepted, AwaitingSpeaker and Done all hold a booked slot: read
    /// that set from <see cref="MeetingRequestStatuses.SlotHolding"/> rather than
    /// re-deriving it, since the database's filtered unique index follows the same set.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>Vestigial: nothing writes a window id here. The column and its foreign key
    /// to <see cref="DelegationAvailabilityWindow"/> exist for parity with
    /// <see cref="SpeakerMeetingRequest.AvailabilityWindowId"/>, which the speaker flow does
    /// populate; here the only writer is the expiry sweep, clearing it back to null.</summary>
    public Guid? AvailabilityWindowId { get; set; }

    /// <summary>The hall, and optionally a meeting table in it, that an approval bound the
    /// meeting to. Null before approval and cleared on cancel or revert, which is what
    /// releases the slot. SetNull, so deleting a hall clears the binding, not the row.</summary>
    public Guid? HallId { get; set; }
    public Hall? Hall { get; set; }

    public Guid? MeetingTableId { get; set; }
    public MeetingTable? MeetingTable { get; set; }

    /// <summary>The admin's note to the requester, and their justification on a cancel.</summary>
    public string? ResponseNote { get; set; }

    /// <summary>When the meeting became confirmed, and by whom; both null until then. The
    /// actor is the admin for a verbal confirmation and the confirming delegate for an app
    /// tap, but stays null when the confirmation arrived through the emailed link, where
    /// the token is the credential and there is no signed-in caller.</summary>
    public DateTime? ConfirmedAt { get; set; }

    public Guid? ConfirmedByUserId { get; set; }

    /// <summary>Stamped by the reminder worker before it dispatches. The null check is
    /// its once-only guard, so a meeting is reminded at most once.</summary>
    public DateTime? ReminderSent { get; set; }

    /// <summary>When an operator checked the meeting in at the hall, which is what moves it
    /// to <see cref="MeetingRequestStatus.Done"/>, and who did it. Both null until then.</summary>
    public DateTime? CheckedInAt { get; set; }

    public Guid? CheckedInByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>The admin who moved the row off Pending. Null while Pending, and reset to
    /// null with <see cref="RespondedAt"/> and the note when the expiry sweep reverts an
    /// approval the other delegation never confirmed, so the admin decides again.</summary>
    public Guid? RespondedByUserId { get; set; }
}
