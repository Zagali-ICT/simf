namespace SIMF.Components.Charts;

/// <summary>
/// One group on a chart's category axis — a forum day, a month, a region — with
/// one value per series, in the chart's fixed series order.
/// </summary>
/// <param name="Label">The category label (already localised).</param>
/// <param name="Values">One value per series. Shorter lists are treated as
/// trailing zeroes; longer lists are truncated to the chart's series count.</param>
public sealed record ChartGroup(string Label, IReadOnlyList<double> Values);

/// <summary>
/// A positioned bar in the chart's viewBox coordinate space (origin top-left,
/// y grows downward, as SVG does).
/// </summary>
public readonly record struct ChartBar(
    double X,
    double Y,
    double Width,
    double Height,
    int GroupIndex,
    int SeriesIndex,
    double Value);

/// <summary>
/// Pure geometry for the SVG charts — no rendering, no state, no framework
/// types, so every rule below is unit-testable in isolation.
///
/// <para>Two rules are enforced here rather than left to each caller: bars are
/// always anchored to a <b>zero baseline</b> (a bar drawn from 95 to 100 would
/// exaggerate a 5% difference), and adjacent bars are separated by a real gap so
/// two fills never touch.</para>
/// </summary>
public static class ChartGeometry
{
    /// <summary>Surface gap between two adjacent bars, in viewBox units.</summary>
    public const double BarGap = 2d;

    /// <summary>Share of each category slot left empty, split either side, so
    /// neighbouring groups read as separate clusters.</summary>
    public const double GroupPadRatio = 0.18d;

    /// <summary>
    /// Rounds an axis maximum up to a readable value — 1, 2, 2.5 or 5 times a
    /// power of ten — so tick labels land on round numbers instead of the raw
    /// data maximum.
    ///
    /// <para>The result is always a whole number. Every chart here plots counts
    /// of people or sessions, and a fractional maximum produces fractional
    /// ticks that then render as repeated integers once formatted.</para>
    /// </summary>
    /// <returns>A nice maximum &gt;= <paramref name="rawMax"/>; 1 when the data
    /// is empty or entirely zero, so an empty chart still draws a sane axis.</returns>
    public static double NiceMax(double rawMax)
    {
        if (double.IsNaN(rawMax) || double.IsInfinity(rawMax) || rawMax <= 0)
        {
            return 1d;
        }

        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawMax)));
        var normalised = rawMax / magnitude;

        var step = normalised switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 2.5d => 2.5d,
            <= 5d => 5d,
            _ => 10d,
        };

        // 2.5 x 10^0 is 2.5 — the only combination that is not already whole.
        return Math.Ceiling(step * magnitude);
    }

    /// <summary>
    /// Picks how many gaps the axis should be divided into so that every tick
    /// lands on a whole number.
    ///
    /// <para>Without this, an axis maximum of 1 divided four ways yields
    /// 0, 0.25, 0.5, 0.75, 1 — which, formatted as counts, reads "0 0 1 1 1".
    /// Preferring more divisions keeps a tall axis well-populated while a
    /// maximum of 1 falls back to a single gap.</para>
    /// </summary>
    public static int PreferredDivisions(double max, int preferred = 4)
    {
        if (max <= 0 || double.IsNaN(max) || double.IsInfinity(max))
        {
            return 1;
        }

        // Try the preferred count first, then neighbours, ending at 1 which
        // always divides evenly.
        foreach (var divisions in new[] { preferred, 5, 3, 2, 1 })
        {
            if (divisions < 1 || divisions > max)
            {
                continue;
            }

            var step = max / divisions;
            if (Math.Abs(step - Math.Round(step)) < 1e-9)
            {
                return divisions;
            }
        }

        return 1;
    }

    /// <summary>
    /// The evenly spaced axis values from 0 to <paramref name="max"/> inclusive.
    /// Always includes the zero baseline.
    /// </summary>
    public static IReadOnlyList<double> AxisTicks(double max, int divisions = 4)
    {
        if (divisions < 1)
        {
            divisions = 1;
        }

        var ticks = new double[divisions + 1];
        for (var i = 0; i <= divisions; i++)
        {
            ticks[i] = max / divisions * i;
        }

        return ticks;
    }

    /// <summary>
    /// Lays out a grouped bar chart.
    /// </summary>
    /// <param name="groups">The category groups, in display order.</param>
    /// <param name="seriesCount">How many bars sit in each group.</param>
    /// <param name="plotWidth">Plot area width in viewBox units.</param>
    /// <param name="plotHeight">Plot area height in viewBox units.</param>
    /// <param name="max">The axis maximum — pass <see cref="NiceMax"/> of the
    /// data maximum. Values of zero or less are treated as 1 so an all-zero
    /// dataset produces flat bars instead of a divide-by-zero.</param>
    /// <param name="rtl">When true the whole plot is mirrored horizontally, so
    /// the first group sits at the right edge and reads right-to-left.</param>
    public static IReadOnlyList<ChartBar> GroupedBars(
        IReadOnlyList<ChartGroup> groups,
        int seriesCount,
        double plotWidth,
        double plotHeight,
        double max,
        bool rtl = false)
    {
        if (groups.Count == 0 || seriesCount < 1 || plotWidth <= 0 || plotHeight <= 0)
        {
            return [];
        }

        // An all-zero (or negative) maximum would divide by zero. Treating it as
        // 1 draws every bar flat on the baseline, which is the honest picture of
        // "no data yet" — not a crash and not a full-height bar.
        if (max <= 0 || double.IsNaN(max) || double.IsInfinity(max))
        {
            max = 1d;
        }

        var slotWidth = plotWidth / groups.Count;
        var slotPad = slotWidth * GroupPadRatio / 2d;
        var innerWidth = slotWidth - (slotPad * 2d);

        // Reserve the inter-bar gaps first, then split what is left. A group
        // narrow enough that the gaps would consume it falls back to touching
        // bars rather than a negative width.
        var totalGap = BarGap * (seriesCount - 1);
        var barWidth = (innerWidth - totalGap) / seriesCount;
        if (barWidth <= 0)
        {
            barWidth = innerWidth / seriesCount;
            totalGap = 0d;
        }

        var gap = seriesCount > 1 ? totalGap / (seriesCount - 1) : 0d;
        var bars = new List<ChartBar>(groups.Count * seriesCount);

        for (var g = 0; g < groups.Count; g++)
        {
            var groupStart = (slotWidth * g) + slotPad;

            for (var s = 0; s < seriesCount; s++)
            {
                var value = s < groups[g].Values.Count ? groups[g].Values[s] : 0d;
                if (double.IsNaN(value) || value < 0)
                {
                    value = 0d;
                }

                var height = Math.Min(value / max, 1d) * plotHeight;
                var x = groupStart + (s * (barWidth + gap));

                // Mirror about the plot's vertical centre line for RTL. This
                // flips both the group order and the bar order inside each
                // group, which is what an Arabic reader expects.
                if (rtl)
                {
                    x = plotWidth - x - barWidth;
                }

                bars.Add(new ChartBar(
                    X: x,
                    // SVG's origin is top-left, so a bar anchored to the zero
                    // baseline starts at (plotHeight - height).
                    Y: plotHeight - height,
                    Width: barWidth,
                    Height: height,
                    GroupIndex: g,
                    SeriesIndex: s,
                    Value: value));
            }
        }

        return bars;
    }

    /// <summary>
    /// The largest value across every group and series — the input to
    /// <see cref="NiceMax"/>. Returns 0 for an empty set.
    /// </summary>
    public static double MaxValue(IReadOnlyList<ChartGroup> groups)
    {
        var max = 0d;
        foreach (var group in groups)
        {
            foreach (var value in group.Values)
            {
                if (!double.IsNaN(value) && value > max)
                {
                    max = value;
                }
            }
        }

        return max;
    }

    /// <summary>
    /// The filled fraction of a horizontal gauge track, clamped to 0..1. A
    /// non-positive maximum yields 0 rather than dividing by zero.
    /// </summary>
    public static double GaugeFraction(double value, double max)
    {
        if (max <= 0 || double.IsNaN(value) || double.IsNaN(max) || value <= 0)
        {
            return 0d;
        }

        return Math.Min(value / max, 1d);
    }
}
