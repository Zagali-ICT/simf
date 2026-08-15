using SIMF.Common.Enums;
using SIMF.Domain.Common;
using SIMF.Domain.Programme;

namespace SIMF.Domain.BusinessMeetings;

/// <summary>A delegation-to-delegation meeting request, mirroring
/// <see cref="SpeakerMeetingRequest"/> but keyed on countries. A repeat submission against
/// the same target moves the existing Pending row, enforced only on the submit path.</summary>
public sealed class DelegationMeetingRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Identity-database user: a bare Guid, never a foreign key.</summary>
    public Guid RequestedByUserId { get; set; }

    /// <summary>The requester's nationality, which need not be invited (so the host can ask
    /// to meet visitors); the target must be invited, and not the same country.</summary>
    public int RequestingCountryId { get; set; }
    public Country? RequestingCountry { get; set; }

    public int TargetCountryId { get; set; }
    public Country? TargetCountry { get; set; }

    public int AttendeeCount { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>Saudi local time, null until proposed. The requester's proposal until an
    /// approval binds a hall slot over it, after which it is the time of record.</summary>
    public DateTime? SlotStart { get; set; }
    public DateTime? SlotEnd { get; set; }

    /// <summary>Accepted, AwaitingSpeaker and Done all hold a booked slot; read that set from
    /// <see cref="MeetingRequestStatuses.SlotHolding"/>, which the filtered index follows.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>Vestigial: nothing writes a window id here; the expiry sweep only clears it.</summary>
    public Guid? AvailabilityWindowId { get; set; }

    /// <summary>Cleared on cancel or revert, which is what releases the slot.</summary>
    public Guid? HallId { get; set; }
    public Hall? Hall { get; set; }

    public Guid? MeetingTableId { get; set; }
    public MeetingTable? MeetingTable { get; set; }

    /// <summary>Shown to the requester.</summary>
    public string? ResponseNote { get; set; }

    /// <summary>The actor is the admin on a verbal confirmation and the delegate on an app tap,
    /// but null when the emailed link was used, where the token is the only credential.</summary>
    public DateTime? ConfirmedAt { get; set; }

    public Guid? ConfirmedByUserId { get; set; }

    /// <summary>The reminder worker's once-only guard, committed before it dispatches.</summary>
    public DateTime? ReminderSent { get; set; }

    /// <summary>Hall check-in is what moves the request to Done.</summary>
    public DateTime? CheckedInAt { get; set; }

    public Guid? CheckedInByUserId { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /// <summary>Reset to null, with <see cref="RespondedAt"/>, when the expiry sweep reverts
    /// an approval the other delegation never confirmed.</summary>
    public Guid? RespondedByUserId { get; set; }
}
