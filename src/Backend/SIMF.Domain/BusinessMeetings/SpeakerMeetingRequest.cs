using SIMF.Common.Enums;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>An attendee's request to meet an opted-in speaker: an admin accepts or
/// rejects, and an accept may bind a hall slot the speaker then confirms. Twin of
/// <see cref="DelegationMeetingRequest"/>.</summary>
public sealed class SpeakerMeetingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SpeakerId { get; set; }
    public Speaker? Speaker { get; set; }

    /// <summary>Identity-database user: a bare Guid, never a foreign key.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>Typed into the form, so it can differ from the requester's profile name.</summary>
    public string RequesterName { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>Null on a topic-only request. Once an accept binds a hall, this pair
    /// holds the bound hall slot: the meeting's time of record.</summary>
    public DateTime? SlotStart { get; set; }
    public DateTime? SlotEnd { get; set; }

    /// <summary>Cleared if the window is deleted; the booked time is <see cref="SlotStart"/>.</summary>
    public Guid? AvailabilityWindowId { get; set; }

    /// <summary>Accepted, AwaitingSpeaker and Done all HOLD the booked slot; read that set from
    /// <see cref="MeetingRequestStatuses.SlotHolding"/>, which the filtered indexes follow.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    public Guid? HallId { get; set; }

    public Guid? MeetingTableId { get; set; }

    /// <summary>Set by the speaker's emailed link, or by an admin recording a verbal answer.</summary>
    public DateTime? SpeakerDecisionAt { get; set; }

    /// <summary>Shown to the requester, so not the place for an internal remark.</summary>
    public string? ResponseNote { get; set; }

    /// <summary>The reminder worker's once-only guard, committed before it dispatches.</summary>
    public DateTime? ReminderSentAt { get; set; }

    // Hall check-in is the only route to Done, and only an Accepted meeting may take it.
    public DateTime? CheckedInAt { get; set; }

    public Guid? CheckedInByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    // Cleared, with the slot and the hall binding, when a closed request is reopened.
    public DateTime? RespondedAt { get; set; }

    public Guid? RespondedByUserId { get; set; }
}
