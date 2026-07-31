using SIMF.Common;
using Xunit;

namespace SIMF.Application.Tests;

/// <summary>
/// Owner decision 2026-07-31 — SIMF stores and displays Saudi local wall-clock
/// only: plain <see cref="DateTime"/>, no <c>DateTimeOffset</c>, no UTC.
///
/// <para>This suite used to prove a UTC-to-Saudi conversion. There is no
/// conversion left, so it proves the opposite and stronger property: <b>a stored
/// value is rendered verbatim.</b> Every case here fails if anyone reintroduces a
/// shift — which is the mistake worth guarding, because a three-hour shift is
/// invisible in a code review and obvious only to the person who misses their
/// meeting.</para>
/// </summary>
public class SaudiTimeTests
{
    [Fact]
    public void Now_is_saudi_wall_clock_three_hours_ahead_of_utc()
    {
        var utcNow = DateTime.UtcNow;

        var now = SimfClock.Now;

        // Compare on the hour-of-day gap rather than equality, so the assertion is
        // not flaky across the few milliseconds between the two reads.
        var gap = now - utcNow;
        Assert.InRange(gap.TotalMinutes, 179, 181);
    }

    [Fact]
    public void Now_is_not_tagged_utc_or_host_local()
    {
        // Unspecified on purpose: the value is neither UTC nor the host's local
        // time, and tagging it either way invites a framework conversion that
        // would silently shift it.
        Assert.Equal(DateTimeKind.Unspecified, SimfClock.Now.Kind);
    }

    [Fact]
    public void Offset_is_plus_three_hours_no_dst()
    {
        Assert.Equal(TimeSpan.FromHours(3), SimfClock.Offset);
        Assert.Equal(SimfClock.Offset, SaudiTime.Offset);
    }

    [Fact]
    public void FormatSaudi_renders_the_stored_value_verbatim()
    {
        // 01:30 on the 21st is STORED as 01:30 on the 21st. Under the old UTC
        // scheme this same render came from 22:30 on the 20th; if anyone puts a
        // conversion back, this case moves and fails.
        var stored = new DateTime(2026, 11, 21, 1, 30, 0);

        Assert.Equal("21-11-2026 01:30 AM", stored.FormatSaudi());
        Assert.Equal("21-11-2026", stored.FormatSaudi(SaudiTime.DateFormat));
    }

    [Fact]
    public void FormatSaudi_does_not_move_a_value_across_the_day_boundary()
    {
        // The trap case: 22:30 must render as the 20th at 10:30 PM, NOT as the
        // 21st at 01:30 AM. That second reading is exactly what a leftover +03:00
        // conversion would produce.
        var lateEvening = new DateTime(2026, 11, 20, 22, 30, 0);

        Assert.Equal("20-11-2026 10:30 PM", lateEvening.FormatSaudi());
        Assert.Equal(20, lateEvening.Day);
    }

    [Fact]
    public void FormatSaudiTime_renders_twelve_hour_am_pm()
    {
        var morning = new DateTime(2026, 11, 21, 1, 30, 0);
        var afternoon = new DateTime(2026, 11, 22, 16, 45, 0);

        Assert.Equal("01:30 AM", morning.FormatSaudiTime());
        Assert.Equal("04:45 PM", afternoon.FormatSaudiTime());
        Assert.Equal("01:30 AM – 04:45 PM", SaudiTime.FormatSaudiWindow(morning, afternoon));
    }

    [Fact]
    public void FormatSaudi_nullable_returns_fallback_for_null()
    {
        DateTime? none = null;

        Assert.Equal(string.Empty, none.FormatSaudi());
        Assert.Equal("—", none.FormatSaudi(SaudiTime.DateTimeFormat, "—"));
    }

    [Fact]
    public void FromSaudiWallClock_stores_exactly_what_was_typed()
    {
        // An admin types 12:00 on 2026-11-20 into a datetime-local field. It must
        // persist as 12:00 on 2026-11-20 — not 09:00, which is what the previous
        // UTC-converting implementation stored.
        var typed = new DateTime(2026, 11, 20, 12, 0, 0);

        var stored = SaudiTime.FromSaudiWallClock(typed);

        Assert.Equal(typed, stored);
        Assert.Equal(12, stored.Hour);
    }

    [Fact]
    public void Save_then_render_round_trips_exactly()
    {
        var typed = new DateTime(2026, 11, 22, 16, 45, 0);

        var stored = SaudiTime.FromSaudiWallClock(typed);
        var rendered = stored.FormatSaudi();

        Assert.Equal("22-11-2026 04:45 PM", rendered);
    }
}
