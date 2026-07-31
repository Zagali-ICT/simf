using SIMF.Common;

namespace SIMF.Common;

/// <summary>
/// The system clock. Owner decision (2026-07-31): SIMF stores and works in
/// <b>Saudi local wall-clock time only</b> — plain <see cref="DateTime"/>, no
/// <c>DateTimeOffset</c>, no UTC anywhere in the database or on the wire.
///
/// <para>Every "now" in the system comes from here. A stored value means exactly
/// what it reads: <c>2026-11-23 09:00</c> is nine in the morning in Riyadh, and
/// no conversion is applied on the way in or on the way out.</para>
///
/// <para>Saudi Arabia observes AST = UTC+03:00 with <b>no daylight saving, ever</b>,
/// so the fixed offset below is exact and permanently stable. It is applied to
/// <see cref="DateTime.UtcNow"/> rather than reading the host's local time on
/// purpose: a server in another timezone (or a CI runner in UTC) must still stamp
/// Saudi time, so the answer cannot depend on where the process happens to run.</para>
/// </summary>
public static class SimfClock
{
    /// <summary>Saudi Standard Time relative to UTC: +03:00, no DST.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    /// <summary>Now, as Saudi wall-clock time. <see cref="DateTimeKind.Unspecified"/>
    /// deliberately: the value is not UTC and is not the host's local time, and
    /// tagging it either way invites a framework conversion that would shift it.</summary>
    public static DateTime Now => DateTime.SpecifyKind(
        DateTime.UtcNow.Add(Offset), DateTimeKind.Unspecified);

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
    /// Replaces <c>GetUtcNow()</c> everywhere: the provider still ticks in UTC
    /// internally (that is its contract), and this is the one place the offset is
    /// applied.</summary>
    public static DateTime SimfNow(this TimeProvider timeProvider) =>
        DateTime.SpecifyKind(
            timeProvider.GetUtcNow().UtcDateTime.Add(SimfClock.Offset),
            DateTimeKind.Unspecified);
}
