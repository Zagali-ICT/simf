using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>
/// An attendee's request to meet a <see cref="Speaker"/> who has opted in via
/// <see cref="Speaker.AllowsMeetingRequests"/>. Created
/// <see cref="MeetingRequestStatus.Pending"/>; an admin accepts or rejects it, and
/// an accept may go further and bind the meeting to a hall slot the speaker then
/// confirms. The twin of <see cref="DelegationMeetingRequest"/>, which runs the
/// same lifecycle against a delegation rather than a speaker.
/// </summary>
public sealed class SpeakerMeetingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>A real foreign key: the speaker lives in the same DbContext.</summary>
    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>A bare Guid rather than a navigation: the user lives in the Identity
    /// database, so there is no foreign key across the two.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>Typed into the form, so it can differ from the requester's profile
    /// name when they are asking on someone else's behalf.</summary>
    public string RequesterName { get; set; } = string.Empty;

    /// <summary>The meeting topic. Free text, capped at 1000 characters.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>The slot the requester picked out of the speaker's availability
    /// windows, checked at submit time to fall inside an active window and to be free.
    /// Null on a topic-only request, which asks for a meeting without naming a time.
    /// Once an accept binds a hall this pair holds the bound hall slot instead: the
    /// meeting's time of record, and what the speaker and hall uniqueness indexes read
    /// as booked. A check constraint keeps the two ordered.</summary>
    public DateTime? SlotStart { get; set; }
    public DateTime? SlotEnd { get; set; }

    /// <summary>The <see cref="SpeakerAvailabilityWindow"/> the picked slot came out
    /// of. Null on a topic-only request, and cleared if that window is later deleted,
    /// so the booked time is <see cref="SlotStart"/>, never this.</summary>
    public Guid? AvailabilityWindowId { get; set; }

    /// <summary>Pending on create. An admin's review lands it on Accepted or Rejected,
    /// or on AwaitingSpeaker when the accept also bound a hall slot the speaker has
    /// still to confirm. Accepted, AwaitingSpeaker and Done all HOLD the booked slot;
    /// <see cref="MeetingRequestStatuses.SlotHolding"/> is the authority for that set,
    /// and every overlap check and index filter has to agree with it.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>The <see cref="Hall"/> an accept bound the meeting to. Null leaves a
    /// confirmed meeting with no room of its own.</summary>
    public Guid? HallId { get; set; }

    /// <summary>The <see cref="MeetingTable"/> inside <see cref="HallId"/> it was
    /// placed at. Null when a hall was bound but no particular table.</summary>
    public Guid? MeetingTableId { get; set; }

    /// <summary>When the speaker's own answer arrived, by following the approve or
    /// reject link emailed to them, or stamped by the admin recording a confirmation
    /// given verbally. Null while the request sits in AwaitingSpeaker.</summary>
    public DateTime? SpeakerDecisionAt { get; set; }

    /// <summary>The admin's note on the decision. It is shown to the requester, so it
    /// is not the place for an internal remark.</summary>
    public string? ResponseNote { get; set; }

    /// <summary>Stamped by the reminder worker 15 minutes ahead of
    /// <see cref="SlotStart"/> and committed before the notification goes out, so the
    /// null check is its once-only guard. The two cannot share a transaction: the
    /// notification rows live in the Identity database.</summary>
    public DateTime? ReminderSent { get; set; }

    // Stamped when an operator checks the meeting in at the hall, the only route to
    // Done and one only an Accepted meeting may take. The operator is a bare Guid for
    // the same reason as RequestedByUserId above.
    public DateTime? CheckedInAt { get; set; }

    public Guid? CheckedInByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    // When the admin moved the row off Pending, and who moved it. Both null while it
    // is still Pending, and both cleared again, along with the slot and the hall
    // binding, when a rejected or cancelled request is reopened.
    public DateTime? RespondedAt { get; set; }

    public Guid? RespondedByUserId { get; set; }
}
