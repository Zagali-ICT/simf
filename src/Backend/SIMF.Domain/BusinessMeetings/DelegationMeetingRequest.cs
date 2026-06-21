using SIMF.Common.Enums;
using SIMF.Domain.Common;

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
    public DateTimeOffset? SlotStartUtc { get; set; }
    public DateTimeOffset? SlotEndUtc { get; set; }

    /// <summary>Lifecycle state. Pending on create; Accepted/Rejected after review.</summary>
    public MeetingRequestStatus Status { get; set; } = MeetingRequestStatus.Pending;

    /// <summary>Optional admin response note shown to the requester.</summary>
    public string? ResponseNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>The admin who responded. Logical FK (Identity); null while Pending.</summary>
    public Guid? RespondedByUserId { get; set; }
}
