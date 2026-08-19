using SIMF.Common.Enums;

namespace SIMF.Contracts.Sessions;

/// <summary>The attendee's device reports its
/// current GPS position to claim arrival at a session's hall. The server checks
/// the point against the hall's geofence and records a
/// <see cref="AttendanceMethod.Geofence"/> arrival. The raw coordinates are NOT
/// stored — only the derived enter/leave times (sensitive PII).</summary>
public sealed class RecordArrivalRequest
{
    public double Lat { get; set; }
    public double Lon { get; set; }
}

/// <summary>The attendee's current attendance state for a session.
/// <see cref="Arrived"/> is true while an open attendance row exists (entered,
/// not yet left). Returned by the arrival, departure, and status endpoints.</summary>
public sealed record HallAttendanceStatus(
    bool Arrived,
    DateTime? Enter,
    DateTime? Leave,
    AttendanceMethod? Method);

/// <summary>The operator's hall-door QR scan — the badge QR id to
/// resolve to the attendee.</summary>
public sealed class RecordQrArrivalRequest
{
    public string QrId { get; set; } = string.Empty;
}

/// <summary>The result of an operator QR-door scan — the resolved
/// attendee (so the operator console can confirm WHO was recorded) plus the
/// resulting attendance state.</summary>
/// <param name="UserId">The attendee's Identity account, or
/// <see cref="Guid.Empty"/> when they hold none — a walk-in or a bulk-minted
/// badge. A SHIPPED field the deployed app decodes, so it keeps both its name
/// and its non-nullable type; <see cref="UserProfileId"/> was appended beside it
/// rather than replacing it. Never look an attendee up by this value: empty here
/// means "no account", and it is a matches-nobody sentinel elsewhere.</param>
/// <param name="UserProfileId">The attendee record itself, which every attendee
/// has and which the attendance row is actually keyed by. Appended after the app
/// shipped, so an older client simply ignores it.</param>
public sealed record QrArrivalResult(
    Guid UserId,
    string DisplayName,
    string DisplayNameArabic,
    HallAttendanceStatus Status,
    Guid UserProfileId);

/// <summary>One selectable session for the hall-arrival console's picker.
///
/// <para>Deliberately NOT <c>AdminSessionSummary</c>. That record is served by
/// <c>/admin/sessions/list</c>, which is gated <c>Sessions.View</c> - a
/// permission the SecurityTeam role that runs this console does not hold, so the
/// console's own first fetch used to 403 for the exact operator it was built
/// for. This carries only what the picker needs and rides the console's own
/// <c>HallArrivals.View</c> gate.</para>
///
/// <para><paramref name="EffectiveArrivalGraceMinutes"/> is the resolved value
/// (session override -> hall -> global -> default), not a raw override. The
/// console filters its picker by it, and the hall door applies the same shared
/// rule, so the two cannot disagree about which sessions are open for
/// arrivals.</para></summary>
public sealed record HallArrivalSessionOption(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    string HallName,
    string HallNameArabic,
    DateTime Start,
    DateTime End,
    int EffectiveArrivalGraceMinutes);
