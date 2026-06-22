// CP Phase-1 — behaviour test for the AI Control Center dashboard: it reads the
// aggregate from GET /admin/ai/dashboard and renders the health tiles + the
// per-service breakdown.
using Bunit;
using Bunit.TestDoubles;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Ai;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class AiDashboardTests : CpComponentTestBase
{
    [Fact]
    public void Renders_health_tiles_and_per_service_rows()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var dash = new AdminAiDashboard(
            WindowHours: 24,
            TotalCalls: 100,
            ErrorCount: 5,
            ErrorRate: 0.05,
            AvgLatencyMs: 320,
            TotalTokens: 12345,
            ActiveServices: 3,
            TotalServices: 7,
            Services: new List<AdminAiServiceStat>
            {
                new(AiFeature.SessionSummary, 40, 2, 0.05, 300, 8000),
                new(AiFeature.Faq, 60, 3, 0.05, 330, 4345),
            });
        JSInterop.Setup<ApiResult<AdminAiDashboard>>(
            inv => inv.Identifier == "simfAccount.getJson")
            .SetResult(ApiResult<AdminAiDashboard>.Ok(dash));

        var cut = RenderComponent<AiDashboard>();

        cut.WaitForAssertion(() =>
        {
            // Top-line tiles (values; titles are resource keys under the test localizer).
            Assert.Contains("100", cut.Markup);     // total calls
            Assert.Contains("5.0%", cut.Markup);    // error rate
            Assert.Contains("320 ms", cut.Markup);  // avg latency
            Assert.Contains("3 / 7", cut.Markup);   // active / total services
            // Per-service breakdown rows.
            Assert.Contains("SessionSummary", cut.Markup);
            Assert.Contains("Faq", cut.Markup);
        });
    }
}
