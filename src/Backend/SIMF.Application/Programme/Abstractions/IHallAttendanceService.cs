using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Sessions;

namespace SIMF.Application.Programme.Abstractions;

/// <summary>Records an attendee's arrival at and departure from a session's
/// hall. Two means: the attendee's own device crossing the GPS geofence
/// (<see cref="RecordGeofenceArrivalAsync"/>, <c>AttendanceMethod.Geofence</c>)
/// and an operator scanning the badge QR at the hall door
/// (<see cref="RecordQrArrivalAsync"/>, <c>AttendanceMethod.QrScan</c>). Both
/// merge into the one open row. Feeds session attendance and the
/// question-gating-on-arrival check.
///
/// <para><b>Two id kinds cross this interface, and both are
/// <see cref="Guid"/>.</b> The self-service methods take the signed-in
/// <c>userId</c> (an Identity account), because that is all a JWT carries; the
/// operator and gate methods take an <c>attendeeProfileId</c>, because the badge
/// in front of them resolves to the profile and its holder may have no account
/// at all. The parameter name is the only thing that tells them apart, so read
/// it before passing an id through.</para></summary>
public interface IHallAttendanceService
{
    /// <summary>Claim arrival at the session's hall from a reported GPS point.
    /// Validates the point against the hall geofence and opens (or
    /// returns the existing open) attendance row. 404 session; 400 when the hall
    /// has no geofence; 403 when the point is outside it.</summary>
    Task<HallAttendanceStatus> RecordGeofenceArrivalAsync(
        Guid userId, Guid sessionId, double lat, double lon,
        CancellationToken cancellationToken = default);

    /// <summary>Close the attendee's open attendance row for the session (idempotent
    /// — a no-op when there is no open row).</summary>
    Task<HallAttendanceStatus> RecordDepartureAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>The attendee's current attendance state for the session (the open
    /// row if present, else the most recent closed row, else not arrived).</summary>
    Task<HallAttendanceStatus> GetStatusAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>An operator records a hall arrival by
    /// scanning an attendee's badge QR at the door. Resolves the QR to the attendee
    /// (<see cref="SIMF.Application.AccessControl.Abstractions.IQrResolver"/>),
    /// requires an Approved, non-locked attendee, and opens (or returns the existing
    /// open) attendance row with <c>Method = QrScan</c> — merging with any geofence
    /// row. The badge resolves straight to the attendee profile, so a walk-in or a
    /// bulk-minted badge with no account behind it is recorded like any other. No
    /// geofence is required (the operator is physically at the door).
    /// 400 unknown/blank QR; 403 non-approved attendee; 404 unknown session.</summary>
    Task<QrArrivalResult> RecordQrArrivalAsync(
        Guid operatorUserId, Guid sessionId, string qrId,
        CancellationToken cancellationToken = default);

    /// <summary>2026-07-18 — an operator records a hall DEPARTURE (check-out) by
    /// scanning an attendee's badge QR, symmetric to <see cref="RecordQrArrivalAsync"/>.
    /// Resolves the QR to the attendee, confirms the session exists, and closes the
    /// attendee's open attendance row (idempotent — a no-op returning Arrived=false
    /// when they are not checked in / already left). No admission re-check: an
    /// attendee already in the hall must always be allowed to leave. 400 unknown/blank
    /// QR; 404 unknown session.</summary>
    Task<QrArrivalResult> RecordQrDepartureAsync(
        Guid operatorUserId, Guid sessionId, string qrId,
        CancellationToken cancellationToken = default);

    /// <summary>A hall-door gate scan feeds hall attendance.
    /// Resolves the session LIVE in <paramref name="hallId"/> right now (active,
    /// within [Start, End] ± the arrival grace, nearest/running-first);
    /// when none is live it records nothing so an out-of-window scan never opens
    /// attendance.
    /// <para>When <paramref name="directionInferred"/> is true (a
    /// <c>DirectionMode.Both</c> gate, whose recorded direction is only an
    /// alternation guess), the action is DERIVED from the attendee's open-row
    /// state — an open row closes it, otherwise a new arrival opens; this keeps a
    /// re-entry from mis-merging. A fixed In/Out gate passes false and keeps its
    /// configured direction: <see cref="ScanDirection.CheckIn"/> opens (or returns
    /// the existing open) row with <c>Method = QrScan</c>,
    /// <see cref="ScanDirection.CheckOut"/> closes it.</para>
    /// Idempotent and safe to call after the gate scan is already committed.
    /// <paramref name="attendeeProfileId"/> is the App <c>UserProfile.Id</c>
    /// (QrResolution.UserProfileId), NOT the Identity SimfUser id — attendance is
    /// keyed by the profile so that an attendee with no account can be recorded.
    /// <para>Returns <c>true</c> only when hall attendance was
    /// ACTUALLY recorded: a row was opened, merged into, or closed. It returns
    /// <c>false</c> when nothing could be recorded — no session was live in the
    /// hall, a check-out found no open row to close, or the arrival's insert was
    /// rejected by the store and no open row could be re-read (the reason is
    /// logged). The gate surfaces that <c>false</c> to the operator as an advisory
    /// notice: entry was still allowed, but the session attendance is not being
    /// counted. Allowing entry is never affected — only the signal.</para></summary>
    Task<bool> RecordGateDoorScanAsync(
        Guid attendeeProfileId, Guid hallId, ScanDirection direction,
        bool directionInferred, Guid operatorUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gate engine step 11.5 — may this attendee ENTER the session
    /// running in this hall right now?
    ///
    /// <para>This is the third of the three gate rules: a main gate requires an
    /// approved account, any gate requires an allowed profile type, and a
    /// SESSION HALL requires the attendee to be registered for the session. That
    /// third rule previously had no implementation — the
    /// <see cref="DenialReasonCode.BookingRequiredMissing"/> value existed as a
    /// reserved hook with no writer, so any valid badge opened every hall.</para>
    ///
    /// <para>Lives here rather than in the gate engine so the "which session is
    /// live in this hall" window stays defined in exactly one place, shared with
    /// <see cref="RecordGateDoorScanAsync"/>.</para>
    ///
    /// <para>The check reads EVERY session the hall is admitting for,
    /// not only the one attendance will bind to. Halls run sessions back to
    /// back, so within the arrival grace an attendee holding a 10:00 booking is
    /// legitimately at the door at 09:50 while the 09:00 session is still
    /// running; testing only the running session denied them, and widening the
    /// grace — the documented lever for exactly that queue — made it worse.</para>
    ///
    /// <para>Callers must apply this only to an ENTRY. A departure is never
    /// blocked: someone already inside must always be able to leave.</para>
    ///
    /// <para><paramref name="attendeeProfileId"/> is the App
    /// <c>UserProfile.Id</c>. Seat reservations are keyed by it too, so an
    /// attendee holding no account can still be found registered.</para>
    /// </summary>
    Task<HallEntryEligibility> CheckHallEntryEligibilityAsync(
        Guid attendeeProfileId, Guid hallId,
        CancellationToken cancellationToken = default);

    /// <summary>The sessions the hall-arrival console may offer in its picker.
    ///
    /// <para>Exists so that console does not have to call the canonical
    /// <c>/admin/sessions/list</c>, which is gated on a permission its operator
    /// role does not hold. Same reason the console has its own contract: the
    /// page's own permission has to be sufficient to load the page.</para>
    ///
    /// <para>Active sessions only, and the grace value is resolved server-side
    /// with the same rule the door applies, so the picker and the door cannot
    /// disagree about which sessions are open for arrivals.</para></summary>
    Task<GridPage<HallArrivalSessionOption>> ListArrivalSessionsAsync(
        GridQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of the session-hall entry check (gate engine step 11.5).
/// </summary>
public enum HallEntryEligibility
{
    /// <summary>No session is live in this hall within the arrival window, so
    /// there is no registration to check. The gate's other rules still apply;
    /// this one simply has nothing to say.</summary>
    NoLiveSession = 0,

    /// <summary>The attendee holds an active seat reservation for the live
    /// session. Admit.</summary>
    Registered = 1,

    /// <summary>The attendee is already inside (an open attendance row), so they
    /// were admitted earlier. Re-scanning must not lock someone out of a hall
    /// they are standing in.</summary>
    AlreadyInside = 2,

    /// <summary>A session is live and the attendee has no reservation for it.
    /// Denied unless the walk-in mode is armed.</summary>
    NotRegistered = 3,
}
