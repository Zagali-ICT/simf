// bUnit render tests for SimfGroupedBarChart. ChartGeometryTests proves the
// arithmetic; these prove the component actually turns that arithmetic into the
// expected DOM — the bars, the legend, the axis, the accessible name and the
// data-table alternative.
using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using SIMF.Components.Charts;

namespace SIMF.ControlPanel.Tests;

public sealed class SimfGroupedBarChartTests : TestContext
{
    private static readonly string[] Series = ["Registered", "Present", "Attended"];

    private static readonly ChartGroup[] ThreeDays =
    [
        new("Day One", [120d, 90d, 70d]),
        new("Day Two", [80d, 140d, 110d]),
        new("Day Three", [40d, 60d, 200d]),
    ];

    private IRenderedComponent<SimfGroupedBarChart> Render(
        IReadOnlyList<ChartGroup> groups, bool? rtl = null) =>
        RenderComponent<SimfGroupedBarChart>(parameters => parameters
            .Add(p => p.Title, "The programme, day by day")
            .Add(p => p.Subtitle, "Across 3 forum days")
            .Add(p => p.Groups, groups)
            .Add(p => p.SeriesLabels, Series)
            .Add(p => p.CategoryLabel, "Forum day")
            .Add(p => p.EmptyLabel, "No programme days yet")
            .Add(p => p.Description, "Comparison of registered, present and attended per day")
            .Add(p => p.Rtl, rtl));

    [Fact]
    public void Renders_one_bar_per_group_and_series()
    {
        var cut = Render(ThreeDays);

        Assert.Equal(9, cut.FindAll(".simf-chart__bar").Count);
    }

    [Fact]
    public void Each_series_wears_its_own_colour_class()
    {
        var cut = Render(ThreeDays);

        // Three bars per series slot, and the slot index drives the class, so a
        // metric keeps its colour no matter how the groups are ordered.
        Assert.Equal(3, cut.FindAll(".simf-chart__bar--1").Count);
        Assert.Equal(3, cut.FindAll(".simf-chart__bar--2").Count);
        Assert.Equal(3, cut.FindAll(".simf-chart__bar--3").Count);
    }

    [Fact]
    public void Bars_never_carry_a_hardcoded_colour()
    {
        // Colour must come from the token stylesheet, never an inline fill.
        var cut = Render(ThreeDays);

        foreach (var bar in cut.FindAll(".simf-chart__bar"))
        {
            Assert.Null(bar.GetAttribute("fill"));
            Assert.Null(bar.GetAttribute("style"));
        }
    }

    [Fact]
    public void A_legend_entry_is_rendered_for_every_series()
    {
        var cut = Render(ThreeDays);

        var labels = cut.FindAll(".simf-chart__legend-label").Select(e => e.TextContent.Trim());

        Assert.Equal(Series, labels);
    }

    [Fact]
    public void The_category_labels_are_rendered_in_group_order()
    {
        var cut = Render(ThreeDays);

        var ticks = cut.FindAll(".simf-chart__xtick").Select(e => e.TextContent.Trim());

        Assert.Equal(["Day One", "Day Two", "Day Three"], ticks);
    }

    [Fact]
    public void Every_value_is_directly_labelled()
    {
        var cut = Render(ThreeDays);

        var values = cut.FindAll(".simf-chart__value").Select(e => e.TextContent.Trim()).ToList();

        Assert.Equal(9, values.Count);
        Assert.Contains("200", values);
        Assert.Contains("120", values);
    }

    [Fact]
    public void The_axis_ticks_are_whole_numbers_and_distinct()
    {
        var cut = Render(ThreeDays);

        var ticks = cut.FindAll(".simf-chart__ytick").Select(e => e.TextContent.Trim()).ToList();

        Assert.NotEmpty(ticks);
        Assert.Equal(ticks.Count, ticks.Distinct().Count());
        Assert.All(ticks, t => Assert.DoesNotContain(".", t, StringComparison.Ordinal));
    }

    [Fact]
    public void The_svg_carries_an_accessible_name_and_description()
    {
        var cut = Render(ThreeDays);

        var svg = cut.Find(".simf-chart__svg");

        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal(
            "Comparison of registered, present and attended per day",
            svg.GetAttribute("aria-label"));
        Assert.Contains("The programme, day by day", cut.Find(".simf-chart__svg title").TextContent);
    }

    [Fact]
    public void A_hidden_data_table_carries_the_same_numbers()
    {
        // The chart must be readable without seeing it, and without separating
        // the series by colour.
        var cut = Render(ThreeDays);

        var table = cut.Find(".simf-chart table.simf-visually-hidden");
        var rows = table.QuerySelectorAll("tbody tr");

        Assert.Equal(3, rows.Length);

        var headers = table.QuerySelectorAll("thead th").Select(e => e.TextContent.Trim());
        Assert.Equal(["Forum day", "Registered", "Present", "Attended"], headers);

        var lastRow = rows[2].QuerySelectorAll("th, td").Select(e => e.TextContent.Trim());
        Assert.Equal(["Day Three", "40", "60", "200"], lastRow);
    }

    [Fact]
    public void Each_bar_exposes_its_group_series_and_value_on_hover()
    {
        var cut = Render(ThreeDays);

        var titles = cut.FindAll(".simf-chart__bar title").Select(e => e.TextContent.Trim()).ToList();

        Assert.Equal(9, titles.Count);
        Assert.Contains(titles, t => t.Contains("Day Three") && t.Contains("Attended") && t.Contains("200"));
    }

    [Fact]
    public void An_empty_programme_shows_the_empty_message_and_no_plot()
    {
        var cut = Render([]);

        Assert.Equal("No programme days yet", cut.Find(".simf-chart__empty").TextContent.Trim());
        Assert.Empty(cut.FindAll(".simf-chart__svg"));
        Assert.Empty(cut.FindAll(".simf-chart__bar"));
    }

    [Fact]
    public void An_all_zero_programme_still_draws_the_plot_and_the_axis()
    {
        // The state before the forum opens: three days, no activity. The chart
        // must still render its structure rather than collapsing or dividing by
        // zero, and the axis must not repeat the same label.
        ChartGroup[] quiet =
        [
            new("Day One", [0d, 0d, 0d]),
            new("Day Two", [0d, 0d, 0d]),
            new("Day Three", [0d, 0d, 0d]),
        ];

        var cut = Render(quiet);

        Assert.Equal(9, cut.FindAll(".simf-chart__bar").Count);
        Assert.All(cut.FindAll(".simf-chart__bar"), bar =>
            Assert.Equal(0d, double.Parse(
                bar.GetAttribute("height")!, CultureInfo.InvariantCulture)));

        var ticks = cut.FindAll(".simf-chart__ytick").Select(e => e.TextContent.Trim()).ToList();
        Assert.Equal(ticks.Count, ticks.Distinct().Count());
        Assert.Equal(["1", "0"], ticks);
    }

    [Fact]
    public void Rtl_mirrors_the_bars_but_not_the_labels()
    {
        var ltr = Render(ThreeDays, rtl: false);
        var rtl = Render(ThreeDays, rtl: true);

        static double FirstGroupLeft(IRenderedComponent<SimfGroupedBarChart> cut) =>
            cut.FindAll(".simf-chart__bar")
                .Take(3)
                .Min(b => double.Parse(b.GetAttribute("x")!, CultureInfo.InvariantCulture));

        static double LastGroupLeft(IRenderedComponent<SimfGroupedBarChart> cut) =>
            cut.FindAll(".simf-chart__bar")
                .Skip(6)
                .Min(b => double.Parse(b.GetAttribute("x")!, CultureInfo.InvariantCulture));

        // LTR: day one on the left. RTL: day one on the right.
        Assert.True(FirstGroupLeft(ltr) < LastGroupLeft(ltr));
        Assert.True(FirstGroupLeft(rtl) > LastGroupLeft(rtl));

        // The label list stays in document order either way — the surrounding
        // flex row is what mirrors it, so mirroring it here too would cancel out.
        var rtlTicks = rtl.FindAll(".simf-chart__xtick").Select(e => e.TextContent.Trim());
        Assert.Equal(["Day One", "Day Two", "Day Three"], rtlTicks);
    }

    [Fact]
    public void Svg_coordinates_stay_invariant_under_an_arabic_culture()
    {
        // An Arabic culture formats 12.5 as "12,5"; emitted into an SVG x
        // attribute that is not a valid number and the geometry collapses.
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            var arabic = new CultureInfo("ar-SA");
            CultureInfo.CurrentCulture = arabic;
            CultureInfo.CurrentUICulture = arabic;

            var cut = Render(ThreeDays, rtl: true);

            foreach (var bar in cut.FindAll(".simf-chart__bar"))
            {
                foreach (var attribute in new[] { "x", "y", "width", "height" })
                {
                    var raw = bar.GetAttribute(attribute)!;
                    Assert.DoesNotContain(",", raw, StringComparison.Ordinal);
                    Assert.True(
                        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                        $"{attribute}='{raw}' is not an invariant number");
                }
            }

            // The label overlay carries its offsets as CSS percentages; a
            // decimal comma there is not a valid length and would stack every
            // label in the corner.
            foreach (var label in cut.FindAll(".simf-chart__value"))
            {
                var style = label.GetAttribute("style")!;
                Assert.Contains("--simf-bar-x", style, StringComparison.Ordinal);
                Assert.Contains("--simf-bar-y", style, StringComparison.Ordinal);
                Assert.DoesNotContain(",", style, StringComparison.Ordinal);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }

    [Fact]
    public void Value_labels_are_html_not_svg_text()
    {
        // preserveAspectRatio="none" scales glyph outlines with the geometry,
        // so a label drawn inside the SVG renders stretched at any container
        // width other than the viewBox width.
        var cut = Render(ThreeDays);

        Assert.Empty(cut.FindAll(".simf-chart__svg text"));
        Assert.Equal(9, cut.FindAll(".simf-chart__values .simf-chart__value").Count);
    }

    [Fact]
    public void A_full_height_bar_puts_its_label_above_the_fill_not_inside_it()
    {
        // The tallest bar reaches the axis maximum. Its label must sit at 100%
        // up the plot and be lifted clear, never painted on top of the
        // saturated series colour where it would be about 1.1:1 contrast.
        var cut = Render([new ChartGroup("Day One", [100d, 50d, 25d])]);

        var labels = cut.FindAll(".simf-chart__value");

        Assert.Contains(
            "--simf-bar-y:100%", labels[0].GetAttribute("style")!, StringComparison.Ordinal);
        Assert.Contains(
            "--simf-bar-y:50%", labels[1].GetAttribute("style")!, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_series_needs_no_legend()
    {
        var cut = RenderComponent<SimfGroupedBarChart>(parameters => parameters
            .Add(p => p.Title, "Sessions per day")
            .Add(p => p.Groups, new ChartGroup[] { new("Day One", [5d]) })
            .Add(p => p.SeriesLabels, new[] { "Sessions" })
            .Add(p => p.CategoryLabel, "Forum day")
            .Add(p => p.EmptyLabel, "None"));

        // The title already names the single series; a one-entry legend is noise.
        Assert.Empty(cut.FindAll(".simf-chart__legend"));
        Assert.Single(cut.FindAll(".simf-chart__bar"));
    }

    [Fact]
    public void A_group_carrying_more_values_than_series_does_not_grow_a_fourth_bar()
    {
        // A fourth bar would have no legend entry and no colour token.
        var cut = Render([new ChartGroup("Day One", [1d, 2d, 3d, 4d])]);

        Assert.Equal(3, cut.FindAll(".simf-chart__bar").Count);
        Assert.Empty(cut.FindAll(".simf-chart__bar--4"));
    }
}
