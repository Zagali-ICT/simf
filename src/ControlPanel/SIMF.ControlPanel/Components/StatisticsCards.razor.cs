using System.Globalization;
using Microsoft.AspNetCore.Components;
using SIMF.Contracts.Statistics;

namespace SIMF.ControlPanel.Components;

/// <summary>§6.16 (NAV-008) — the eleven live stat cards, in ONE place. They
/// were duplicated verbatim between the Dashboard ("/") and the Statistics page
/// ("/admin/statistics").</summary>
public partial class StatisticsCards
{
    /// <summary>The live aggregate read from GET /account/api/admin/statistics.</summary>
    [Parameter, EditorRequired]
    public SIMF.Contracts.Statistics.StatisticsDashboard Dashboard { get; set; } = default!;

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
