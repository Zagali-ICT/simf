// D-758 (#10 Phase 2) — behaviour test for the persisted bulk-badge batches page
// (BadgeBatchesPage). Stubs the list endpoint and asserts the page loads the batches
// and renders each row's contents + active/revoked status. The re-email / revoke
// actions are exercised end-to-end by the API integration tests
// (SIMF.Api.Tests/DelegatesAndBulkBadgesTests).
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class BadgeBatchesPageTests : CpComponentTestBase
{
    private static readonly Guid ActiveId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RevokedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private IRenderedComponent<BadgeBatchesPage> RenderWithBatches()
    {
        // Grant the ViewBatches policy so the AuthorizedAction-wrapped row actions
        // (re-email / revoke, incl. their SimfIcons) actually render. The live check
        // found an invalid icon name ("ban") only crashes the circuit once a populated
        // row shows its actions — which the default (no-policy) auth context had hidden.
        Authorization.SetPolicies(PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ViewBatches));
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("badge-batches/list"))
            .SetResult(ApiResult<GridPage<AdminBadgeBatchSummary>>.Ok(
                GridPage<AdminBadgeBatchSummary>.Of(
                    new List<AdminBadgeBatchSummary>
                    {
                        new(ActiveId, "VIP × 3", 3, false, "org@simf.test", DateTimeOffset.UtcNow, true),
                        new(RevokedId, "Normal × 2", 2, true, null, DateTimeOffset.UtcNow, false),
                    },
                    total: 2, skip: 0, top: 20)));
        return RenderComponent<BadgeBatchesPage>();
    }

    [Fact]
    public void Loads_and_lists_the_batches_with_status()
    {
        var cut = RenderWithBatches();
        // Both batches' contents are rendered from the stubbed list.
        Assert.Contains("VIP × 3", cut.Markup);
        Assert.Contains("Normal × 2", cut.Markup);
        // Status reflects IsActive (active vs revoked) via the pass-through resx keys.
        Assert.Contains("Admin.BadgeBatches.Status.Active", cut.Markup);
        Assert.Contains("Admin.BadgeBatches.Status.Revoked", cut.Markup);
        // The active batch renders its re-email + revoke actions (exercises the icons,
        // so an unknown SimfIcon name would throw here — the D-758 regression guard).
        Assert.Contains("Admin.BadgeBatches.ReEmail", cut.Markup);
        Assert.Contains("Admin.BadgeBatches.Revoke", cut.Markup);
    }
}
