using SIMF.Common;

namespace SIMF.Common;

/// <summary>
/// The system clock. Owner decision (2026-07-31): SIMF stores and works in
/// <b>Saudi local wall-clock time only</b> — plain <see cref="DateTime"/>, no
/// <c>DateTimeOffset</c>, no a zoned value anywhere in the database or on the wire.
///
/// <para>Every "now" in the system comes from here. A stored value means exactly
/// what it reads: <c>2026-11-23 09:00</c> is nine in the morning in Riyadh, and
/// no conversion is applied on the way in or on the way out.</para>
///
/// <para>Saudi Arabia observes AST = +03:00 with <b>no daylight saving, ever</b>,
/// so the fixed offset below is exact and permanently stable. "Now" is taken as
/// an instant and re-expressed at that offset rather than read off the host's
/// local clock, on purpose: a server in another timezone, or a CI runner
/// elsewhere, must still stamp Saudi time, so the answer cannot depend on where
/// the process happens to run.</para>
/// </summary>
public static class SimfClock
{
    /// <summary>Saudi Standard Time: +03:00, no DST, ever.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    /// <summary>Now, as Saudi wall-clock time.
    /// <para><c>DateTimeOffset.Now</c> captures the current instant together with
    /// whatever offset the host runs at; <c>ToOffset</c> re-expresses that SAME
    /// instant at +03:00 and <c>.DateTime</c> takes the wall-clock reading off it,
    /// already <see cref="DateTimeKind.Unspecified"/>. Host-independent: a server
    /// in any timezone returns the same Riyadh reading. The Kind is left
    /// unspecified deliberately, because tagging it invites a framework
    /// conversion that would shift it.</para></summary>
    public static DateTime Now => DateTimeOffset.Now.ToOffset(Offset).DateTime;

    /// <summary>Today's Saudi calendar date.</summary>
    public static DateTime Today => Now.Date;

    /// <summary>Today's Saudi calendar date as a <see cref="DateOnly"/>.</summary>
    public static DateOnly TodayDate => DateOnly.FromDateTime(Now);
}

/// <summary>Saudi-clock reads from an injected <see cref="TimeProvider"/>, so a
/// test can control "now" through the same seam production uses.</summary>
public static class SimfClockExtensions
{
    /// <summary>Now, as Saudi wall-clock time, from <paramref name="timeProvider"/>.
    /// <para>Same shape as <see cref="SimfClock.Now"/>: take the instant the
    /// provider reports, re-express it at +03:00, read the wall clock off it. The
    /// provider's own zone is irrelevant — <c>ToOffset</c> preserves the instant —
    /// so a fake provider in a test and a real one in production agree.</para></summary>
    public static DateTime SimfNow(this TimeProvider timeProvider) =>
        timeProvider.GetLocalNow().ToOffset(SimfClock.Offset).DateTime;
}
