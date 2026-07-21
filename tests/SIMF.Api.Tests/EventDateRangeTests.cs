// D-755 — unit tests for the shared EventDateRange formatter (SIMF.Common): the
// single bilingual "forum dates" label that drives every surface from the
// CP-editable OrganizationProfile.EventStartDate / EventEndDate.
using System.Globalization;
using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class EventDateRangeTests
{
    [Fact]
    public void Same_month_collapses_to_one_month_and_year_in_English()
    {
        var text = EventDateRange.Format(
            new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 25), arabic: false);

        Assert.Equal("23-25 November 2026", text);
    }

    [Fact]
    public void Same_month_collapses_to_one_month_and_year_in_Arabic()
    {
        var text = EventDateRange.Format(
            new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 25), arabic: true);

        Assert.Equal("23-25 نوفمبر 2026", text);
    }

    [Fact]
    public void Cross_month_same_year_spells_out_both_endpoints_in_English()
    {
        var text = EventDateRange.Format(
            new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 2), arabic: false);

        Assert.Equal("30 November - 2 December 2026", text);
    }

    [Fact]
    public void Cross_month_same_year_spells_out_both_endpoints_in_Arabic()
    {
        var text = EventDateRange.Format(
            new DateOnly(2026, 11, 30), new DateOnly(2026, 12, 2), arabic: true);

        Assert.Equal("30 نوفمبر - 2 ديسمبر 2026", text);
    }

    [Fact]
    public void Cross_year_spells_out_both_years()
    {
        var text = EventDateRange.Format(
            new DateOnly(2025, 12, 30), new DateOnly(2026, 1, 2), arabic: false);

        Assert.Equal("30 December 2025 - 2 January 2026", text);
    }

    [Fact]
    public void Single_day_renders_one_date()
    {
        var text = EventDateRange.Format(
            new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 23), arabic: false);

        Assert.Equal("23 November 2026", text);
    }

    [Fact]
    public void A_reversed_range_is_ordered_before_formatting()
    {
        var text = EventDateRange.Format(
            new DateOnly(2026, 11, 25), new DateOnly(2026, 11, 23), arabic: false);

        Assert.Equal("23-25 November 2026", text);
    }

    [Fact]
    public void Digits_stay_Western_even_under_an_Arabic_thread_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");

            var text = EventDateRange.Format(
                new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 25), arabic: true);

            Assert.Equal("23-25 نوفمبر 2026", text);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
