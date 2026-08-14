namespace SIMF.Contracts.Gates;

/// <summary>
/// The people a door is expecting, downloaded so a scanner can decide entry with
/// no network at all.
///
/// <para>The badge already lets a device answer "is this genuine", "is this from
/// the open year" and "is this tier allowed at this gate". It cannot answer "is
/// this person admitted" or "do they hold a seat in THIS session", so a hall
/// door had to abstain on both — and an abstention at a hall door is a queue.
/// This is the missing third.</para>
///
/// <para><b>Scoped by SEAT RESERVATION, not by attendee list.</b> That is what
/// makes it affordable: the set is bounded by hall capacity rather than event
/// size, so a 400-seat hall downloads at most 400 people however many thousands
/// attend — and it is exactly the set the door is asked about, so nothing
/// irrelevant travels.</para>
/// </summary>
/// <param name="IssuedAt">When the server built this roster. Doubles as the
/// since-cursor for the next delta fetch: a full roster on every gate-console
/// load would not survive a venue's network.</param>
/// <param name="ValidUntil">When the device must stop trusting it. A stale
/// roster admits someone approved this morning and disabled since, which is the
/// failure the abstention existed to prevent, so the expiry is explicit rather
/// than left to the client's judgement.</param>
/// <param name="Attendees">The people expected, one row per reservation.</param>
public sealed record GateOfflineRoster(
    DateTime IssuedAt,
    DateTime ValidUntil,
    IReadOnlyList<GateOfflineRosterEntry> Attendees);

/// <summary>
/// One expected attendee, carrying the MINIMUM a door needs: a name to show the
/// operator and enough to make the decision.
///
/// <para>Deliberately no identity-document number, no mobile, no email, no
/// organisation. Those columns are encrypted at rest precisely so they do not
/// travel, and a gate needs a decision, not a personal record.</para>
/// </summary>
/// <param name="UserProfileId">The attendee, which is the id the badge carries
/// and the id every attendee has with or without an app account.</param>
/// <param name="Name">The name to show the operator.</param>
/// <param name="NameArabic">Its Arabic twin — Arabic is the primary language at
/// the door.</param>
/// <param name="ProfileTypeCode">The tier, so the device can cross-check the
/// badge it just decrypted against the roster row.</param>
/// <param name="IsAdmitted">Whether the attendee's own admission state allows
/// entry. Sent as a decided boolean rather than the raw state: the device should
/// not be reimplementing admission rules that the server already owns.</param>
/// <param name="SessionId">The session they are expected in.</param>
/// <param name="SessionStart">Session start, so the door can judge arrival
/// timing without a clock lookup.</param>
/// <param name="SessionEnd">Session end.</param>
/// <param name="HallId">The hall, so a device serving several doors can filter
/// without another round-trip.</param>
/// <param name="RowLabel">Their seat row, or null for general admission and for
/// a hall admitted by booking rather than by seat.</param>
/// <param name="SeatNumber">Their seat number, on the same terms.</param>
public sealed record GateOfflineRosterEntry(
    Guid UserProfileId,
    string Name,
    string NameArabic,
    short ProfileTypeCode,
    bool IsAdmitted,
    Guid SessionId,
    DateTime SessionStart,
    DateTime SessionEnd,
    Guid HallId,
    string? RowLabel,
    int? SeatNumber);
