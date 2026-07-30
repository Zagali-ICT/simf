// Pure-geometry tests for the dashboard charts (SimfGroupedBarChart /
// SimfBarGauge). No rendering and no DB — these guard the arithmetic that
// decides whether a chart tells the truth: a zero baseline, honest scaling,
// no divide-by-zero on an empty event, and a correct RTL mirror.
using System.Globalization;
using SIMF.Components.Charts;

namespace SIMF.ControlPanel.Tests;

public sealed class ChartGeometryTests
{
    private const double PlotWidth = 640d;
    private const double PlotHeight = 260d;

    private static readonly string[] ThreeSeries = ["Registered", "Present", "Attended"];

    // -- NiceMax ------------------------------------------------------------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 10)]
    [InlineData(12, 20)]
    [InlineData(23, 25)]
    [InlineData(45, 50)]
    [InlineData(100, 100)]
    [InlineData(120, 200)]
    [InlineData(1750, 2000)]
    public void NiceMax_rounds_up_to_a_readable_axis_maximum(double raw, double expected) =>
        Assert.Equal(expected, ChartGeometry.NiceMax(raw));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NiceMax_falls_back_to_one_for_unusable_input(double raw) =>
        Assert.Equal(1d, ChartGeometry.NiceMax(raw));

    [Fact]
    public void NiceMax_is_never_below_the_data_maximum()
    {
        // A maximum below the data would clip a bar out of the plot.
        for (var raw = 1; raw <= 500; raw++)
        {
            Assert.True(
                ChartGeometry.NiceMax(raw) >= raw,
                $"NiceMax({raw}) returned less than the data maximum");
        }
    }

    // -- Axis ticks ---------------------------------------------------------

    [Fact]
    public void AxisTicks_span_zero_to_max_inclusive()
    {
        var ticks = ChartGeometry.AxisTicks(200, divisions: 4);

        Assert.Equal(5, ticks.Count);
        Assert.Equal(0d, ticks[0]);
        Assert.Equal(200d, ticks[^1]);
        Assert.Equal([0d, 50d, 100d, 150d, 200d], ticks);
    }

    [Fact]
    public void AxisTicks_survives_a_zero_division_request() =>
        Assert.Equal(2, ChartGeometry.AxisTicks(100, divisions: 0).Count);

    // -- Whole-number ticks (regression) -------------------------------------
    // Found on a live render: with no data the axis maximum is 1, and four
    // divisions gave 0, 0.25, 0.5, 0.75, 1 — which formatted as counts read
    // "0 0 1 1 1" down the side of the chart.

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    [InlineData(25)]
    [InlineData(50)]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(2000)]
    public void Every_axis_tick_is_a_whole_number(double rawMax)
    {
        var max = ChartGeometry.NiceMax(rawMax);
        var ticks = ChartGeometry.AxisTicks(max, ChartGeometry.PreferredDivisions(max));

        Assert.All(ticks, tick =>
            Assert.Equal(tick, Math.Round(tick), precision: 9));
    }

    [Fact]
    public void Axis_tick_labels_are_never_duplicated()
    {
        // The user-visible symptom: distinct ticks must not collapse to the
        // same rendered label.
        for (var rawMax = 1; rawMax <= 300; rawMax++)
        {
            var max = ChartGeometry.NiceMax(rawMax);
            var ticks = ChartGeometry.AxisTicks(max, ChartGeometry.PreferredDivisions(max));
            var rendered = ticks
                .Select(t => t.ToString("#,##0", CultureInfo.InvariantCulture))
                .ToList();

            Assert.Equal(rendered.Count, rendered.Distinct().Count());
        }
    }

    [Fact]
    public void An_empty_chart_gets_a_zero_to_one_axis()
    {
        var max = ChartGeometry.NiceMax(0);

        var ticks = ChartGeometry.AxisTicks(max, ChartGeometry.PreferredDivisions(max));

        Assert.Equal([0d, 1d], ticks);
    }

    [Fact]
    public void NiceMax_always_returns_a_whole_number()
    {
        // 2.5 x 10^0 was the one combination that came back fractional.
        for (var raw = 1; raw <= 300; raw++)
        {
            var max = ChartGeometry.NiceMax(raw);
            Assert.Equal(max, Math.Round(max), precision: 9);
        }

        Assert.Equal(3d, ChartGeometry.NiceMax(2.3));
    }

    [Fact]
    public void PreferredDivisions_never_exceeds_the_axis_maximum()
    {
        // Two divisions on a maximum of 1 would put a tick at 0.5.
        Assert.Equal(1, ChartGeometry.PreferredDivisions(1));
        Assert.Equal(2, ChartGeometry.PreferredDivisions(2));
        Assert.Equal(3, ChartGeometry.PreferredDivisions(3));
    }

    [Fact]
    public void PreferredDivisions_survives_a_zero_or_broken_maximum()
    {
        Assert.Equal(1, ChartGeometry.PreferredDivisions(0));
        Assert.Equal(1, ChartGeometry.PreferredDivisions(-4));
        Assert.Equal(1, ChartGeometry.PreferredDivisions(double.NaN));
    }

    // -- Empty and degenerate input ----------------------------------------

    [Fact]
    public void No_groups_produces_no_bars() =>
        Assert.Empty(ChartGeometry.GroupedBars([], 3, PlotWidth, PlotHeight, 10));

    [Fact]
    public void An_all_zero_dataset_draws_flat_bars_rather_than_dividing_by_zero()
    {
        // The event before it opens: every figure is zero. NiceMax(0) is 1, so
        // the axis is sane and every bar sits flat on the baseline.
        var groups = new[]
        {
            new ChartGroup("Day 1", [0d, 0d, 0d]),
            new ChartGroup("Day 2", [0d, 0d, 0d]),
        };
        var max = ChartGeometry.NiceMax(ChartGeometry.MaxValue(groups));

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, max);

        Assert.Equal(6, bars.Count);
        Assert.All(bars, bar =>
        {
            Assert.Equal(0d, bar.Height);
            Assert.False(double.IsNaN(bar.Height));
            Assert.False(double.IsNaN(bar.Y));
            Assert.Equal(PlotHeight, bar.Y);
        });
    }

    [Fact]
    public void A_zero_max_is_treated_as_one_instead_of_dividing_by_zero()
    {
        var groups = new[] { new ChartGroup("Day 1", [5d, 0d, 0d]) };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, max: 0);

        Assert.All(bars, bar => Assert.False(double.IsNaN(bar.Height)));
        // 5 against a fallback max of 1 clamps to a full-height bar.
        Assert.Equal(PlotHeight, bars[0].Height);
    }

    [Fact]
    public void A_single_group_still_lays_out()
    {
        var groups = new[] { new ChartGroup("Day 1", [10d, 5d, 2d]) };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10);

        Assert.Equal(3, bars.Count);
        Assert.All(bars, bar => Assert.True(bar.Width > 0));
    }

    [Fact]
    public void A_group_with_fewer_values_than_series_pads_with_zeroes()
    {
        var groups = new[] { new ChartGroup("Day 1", [10d]) };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10);

        Assert.Equal(3, bars.Count);
        Assert.Equal(10d, bars[0].Value);
        Assert.Equal(0d, bars[1].Value);
        Assert.Equal(0d, bars[2].Value);
    }

    [Fact]
    public void A_negative_value_is_floored_at_zero_rather_than_drawn_upward()
    {
        var groups = new[] { new ChartGroup("Day 1", [-4d, 10d, 0d]) };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10);

        Assert.Equal(0d, bars[0].Value);
        Assert.Equal(0d, bars[0].Height);
    }

    // -- Scaling and the zero baseline --------------------------------------

    [Fact]
    public void Bars_are_anchored_to_the_zero_baseline()
    {
        // The rule a bar chart lives by: every bar starts at zero. If Y+Height
        // ever drifts off the baseline the chart is exaggerating differences.
        var groups = new[]
        {
            new ChartGroup("Day 1", [95d, 100d, 20d]),
            new ChartGroup("Day 2", [40d, 0d, 100d]),
        };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 100);

        Assert.All(bars, bar =>
            Assert.Equal(PlotHeight, bar.Y + bar.Height, precision: 6));
    }

    [Fact]
    public void Bar_height_is_proportional_to_the_axis_maximum()
    {
        var groups = new[] { new ChartGroup("Day 1", [100d, 50d, 25d]) };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 100);

        Assert.Equal(PlotHeight, bars[0].Height, precision: 6);
        Assert.Equal(PlotHeight / 2, bars[1].Height, precision: 6);
        Assert.Equal(PlotHeight / 4, bars[2].Height, precision: 6);
    }

    [Fact]
    public void A_value_above_the_axis_maximum_is_clamped_into_the_plot()
    {
        var groups = new[] { new ChartGroup("Day 1", [500d, 0d, 0d]) };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 100);

        Assert.Equal(PlotHeight, bars[0].Height);
        Assert.True(bars[0].Y >= 0, "a clamped bar must not start above the plot");
    }

    // -- Layout -------------------------------------------------------------

    [Fact]
    public void Bars_within_a_group_never_overlap()
    {
        var groups = new[]
        {
            new ChartGroup("Day 1", [10d, 10d, 10d]),
            new ChartGroup("Day 2", [10d, 10d, 10d]),
            new ChartGroup("Day 3", [10d, 10d, 10d]),
        };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10);

        foreach (var group in bars.GroupBy(b => b.GroupIndex))
        {
            var ordered = group.OrderBy(b => b.X).ToList();
            for (var i = 1; i < ordered.Count; i++)
            {
                Assert.True(
                    ordered[i].X >= ordered[i - 1].X + ordered[i - 1].Width,
                    $"bars {i - 1} and {i} overlap in group {group.Key}");
            }
        }
    }

    [Fact]
    public void Groups_stay_inside_the_plot_and_do_not_collide()
    {
        var groups = new[]
        {
            new ChartGroup("Day 1", [10d, 10d, 10d]),
            new ChartGroup("Day 2", [10d, 10d, 10d]),
            new ChartGroup("Day 3", [10d, 10d, 10d]),
        };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10);

        Assert.All(bars, bar =>
        {
            Assert.True(bar.X >= 0, "a bar started left of the plot");
            Assert.True(bar.X + bar.Width <= PlotWidth + 0.001, "a bar ran past the plot");
        });

        // The last bar of a group sits left of the first bar of the next.
        var groupRight = bars.GroupBy(b => b.GroupIndex)
            .OrderBy(g => g.Key)
            .Select(g => (Left: g.Min(b => b.X), Right: g.Max(b => b.X + b.Width)))
            .ToList();

        for (var i = 1; i < groupRight.Count; i++)
        {
            Assert.True(
                groupRight[i].Left > groupRight[i - 1].Right,
                $"group {i} overlaps group {i - 1}");
        }
    }

    [Fact]
    public void Every_group_and_series_index_is_reported()
    {
        var groups = new[]
        {
            new ChartGroup("Day 1", [1d, 2d, 3d]),
            new ChartGroup("Day 2", [4d, 5d, 6d]),
        };

        var bars = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10);

        Assert.Equal(6, bars.Count);
        Assert.Equal([1d, 2d, 3d, 4d, 5d, 6d], bars.Select(b => b.Value));
        Assert.Equal([0, 0, 0, 1, 1, 1], bars.Select(b => b.GroupIndex));
        Assert.Equal([0, 1, 2, 0, 1, 2], bars.Select(b => b.SeriesIndex));
    }

    // -- RTL ----------------------------------------------------------------

    [Fact]
    public void Rtl_mirrors_the_plot_horizontally()
    {
        var groups = new[]
        {
            new ChartGroup("Day 1", [10d, 20d, 30d]),
            new ChartGroup("Day 2", [40d, 50d, 60d]),
        };

        var ltr = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 60);
        var rtl = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 60, rtl: true);

        Assert.Equal(ltr.Count, rtl.Count);
        for (var i = 0; i < ltr.Count; i++)
        {
            // Same bar, mirrored about the plot's vertical centre line.
            Assert.Equal(PlotWidth - ltr[i].X - ltr[i].Width, rtl[i].X, precision: 6);
            // Vertical geometry and identity are untouched by direction.
            Assert.Equal(ltr[i].Y, rtl[i].Y, precision: 6);
            Assert.Equal(ltr[i].Height, rtl[i].Height, precision: 6);
            Assert.Equal(ltr[i].Value, rtl[i].Value);
        }
    }

    [Fact]
    public void Rtl_puts_the_first_group_on_the_right()
    {
        var groups = new[]
        {
            new ChartGroup("Day 1", [10d, 10d, 10d]),
            new ChartGroup("Day 2", [10d, 10d, 10d]),
        };

        var rtl = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10, rtl: true);

        var firstGroupLeft = rtl.Where(b => b.GroupIndex == 0).Min(b => b.X);
        var secondGroupLeft = rtl.Where(b => b.GroupIndex == 1).Min(b => b.X);

        Assert.True(
            firstGroupLeft > secondGroupLeft,
            "in RTL the first group must sit to the right of the second");
    }

    [Fact]
    public void Rtl_bars_stay_inside_the_plot()
    {
        var groups = new[]
        {
            new ChartGroup("Day 1", [10d, 10d, 10d]),
            new ChartGroup("Day 2", [10d, 10d, 10d]),
            new ChartGroup("Day 3", [10d, 10d, 10d]),
        };

        var rtl = ChartGeometry.GroupedBars(groups, 3, PlotWidth, PlotHeight, 10, rtl: true);

        Assert.All(rtl, bar =>
        {
            Assert.True(bar.X >= -0.001, "an RTL bar started left of the plot");
            Assert.True(bar.X + bar.Width <= PlotWidth + 0.001, "an RTL bar ran past the plot");
        });
    }

    // -- MaxValue -----------------------------------------------------------

    [Fact]
    public void MaxValue_of_an_empty_set_is_zero() =>
        Assert.Equal(0d, ChartGeometry.MaxValue([]));

    [Fact]
    public void MaxValue_spans_every_group_and_series()
    {
        var groups = new[]
        {
            new ChartGroup("Day 1", [10d, 220d, 30d]),
            new ChartGroup("Day 2", [40d, 50d, 60d]),
        };

        Assert.Equal(220d, ChartGeometry.MaxValue(groups));
    }

    // -- Gauge --------------------------------------------------------------

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 0.5)]
    [InlineData(100, 100, 1)]
    [InlineData(150, 100, 1)]   // clamped
    [InlineData(-5, 100, 0)]    // floored
    [InlineData(10, 0, 0)]      // no divide-by-zero
    [InlineData(10, -1, 0)]
    public void GaugeFraction_clamps_between_zero_and_one(
        double value, double max, double expected) =>
        Assert.Equal(expected, ChartGeometry.GaugeFraction(value, max));

    [Fact]
    public void GaugeFraction_never_returns_NaN()
    {
        Assert.Equal(0d, ChartGeometry.GaugeFraction(double.NaN, 100));
        Assert.Equal(0d, ChartGeometry.GaugeFraction(10, double.NaN));
    }

    // -- The chart's own contract -------------------------------------------

    [Fact]
    public void The_series_count_drives_the_bar_count_not_the_value_list_length()
    {
        // A group carrying more values than the chart has series must not emit
        // a fourth bar with no legend entry and no colour token.
        var groups = new[] { new ChartGroup("Day 1", [1d, 2d, 3d, 4d, 5d]) };

        var bars = ChartGeometry.GroupedBars(
            groups, ThreeSeries.Length, PlotWidth, PlotHeight, 10);

        Assert.Equal(ThreeSeries.Length, bars.Count);
    }
}
