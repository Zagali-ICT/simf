using SIMF.Common;
using Xunit;

namespace SIMF.Application.Tests;

/// <summary>
/// Proves the single UTC↔Saudi conversion point: stored UTC renders on the +03:00
/// wall clock, the day boundary lands on the correct local calendar day, nulls are
/// safe, and the datetime-local save path round-trips back to the exact UTC instant.
/// </summary>
public class SaudiTimeTests
{
    [Fact]
    public void Offset_is_plus_three_hours_no_dst()
    {
        Assert.Equal(TimeSpan.FromHours(3), SaudiTime.Offset);
    }

    [Fact]
    public void ToSaudi_shifts_utc_by_three_hours()
    {
        var utc = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);

        var saudi = utc.ToSaudi();

        Assert.Equal(TimeSpan.FromHours(3), saudi.Offset);
        Assert.Equal(12, saudi.Hour);          // 09:00 UTC == 12:00 AST
        Assert.Equal(utc, saudi);              // same instant, different offset
    }

    [Fact]
    public void ToSaudi_near_midnight_lands_on_next_local_day()
    {
        // 22:30 UTC on the 20th is 01:30 AST on the 21st — a real day-boundary trap.
        var utc = new DateTimeOffset(2026, 11, 20, 22, 30, 0, TimeSpan.Zero);

        var saudi = utc.ToSaudi();

        Assert.Equal(21, saudi.Day);
        Assert.Equal(1, saudi.Hour);
        Assert.Equal(30, saudi.Minute);
    }

    [Fact]
    public void ToSaudi_nullable_passes_null_through()
    {
        DateTimeOffset? none = null;
        Assert.Null(none.ToSaudi());

        DateTimeOffset? some = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromHours(3), some.ToSaudi()!.Value.Offset);
    }

    [Fact]
    public void FormatSaudi_uses_local_wall_clock_and_invariant_culture()
    {
        var utc = new DateTimeOffset(2026, 11, 20, 22, 30, 0, TimeSpan.Zero);

        Assert.Equal("2026-11-21 01:30 AM", utc.FormatSaudi());
        Assert.Equal("2026-11-21", utc.FormatSaudi(SaudiTime.DateFormat));
    }

    [Fact]
    public void FormatSaudiTime_renders_saudi_twelve_hour_am_pm()
    {
        var morning = new DateTimeOffset(2026, 11, 20, 22, 30, 0, TimeSpan.Zero);   // 01:30 AST
        var afternoon = new DateTimeOffset(2026, 11, 22, 13, 45, 0, TimeSpan.Zero); // 04:45 PM AST

        Assert.Equal("01:30 AM", morning.FormatSaudiTime());
        Assert.Equal("04:45 PM", afternoon.FormatSaudiTime());
        Assert.Equal("01:30 AM – 04:45 PM", SaudiTime.FormatSaudiWindow(morning, afternoon));
    }

    [Fact]
    public void FormatSaudi_nullable_returns_fallback_for_null()
    {
        DateTimeOffset? none = null;

        Assert.Equal(string.Empty, none.FormatSaudi());
        Assert.Equal("—", none.FormatSaudi(SaudiTime.DateTimeFormat, "—"));
    }

    [Fact]
    public void FromSaudiWallClock_converts_typed_local_time_back_to_utc()
    {
        // An admin types 12:00 on 2026-11-20 into a datetime-local field (Saudi wall
        // clock, no zone). It must persist as 09:00 UTC.
        var typed = new DateTime(2026, 11, 20, 12, 0, 0);

        var utc = SaudiTime.FromSaudiWallClock(typed);

        Assert.Equal(TimeSpan.Zero, utc.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Save_then_render_round_trips_exactly()
    {
        var typed = new DateTime(2026, 11, 22, 16, 45, 0);

        var storedUtc = SaudiTime.FromSaudiWallClock(typed);
        var rendered = storedUtc.FormatSaudi();

        Assert.Equal("2026-11-22 04:45 PM", rendered);
    }
}
