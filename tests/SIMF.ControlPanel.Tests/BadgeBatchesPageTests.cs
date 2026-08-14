// D-758 (#10 Phase 2) — behaviour test for the persisted bulk-badge batches page
// (BadgeBatchesPage). Stubs the list endpoint and asserts the page loads the batches
// and renders each row's contents + active/revoked status. The re-email / revoke
// actions are exercised end-to-end by the API integration tests
// (SIMF.Api.Tests/DelegatesAndBulkBadgesTests).
using System.Globalization;
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class BadgeBatchesPageTests : CpComponentTestBase
{
    private static readonly Guid ActiveId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RevokedId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid VipTypeId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    /// <summary>Stubs the visitor profile-type lookup the top-up picker loads.</summary>
    private void StubProfileTypes() =>
        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
                "simfAccount.getJson",
                inv => ((string)inv.Arguments[0]!).Contains("profile-types"))
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                new List<AdminProfileTypeSummary>
                {
                    // Id, Name, NameArabic, PageColor, UserType, MobileAppRole,
                    // IsActive, IsVisitor, IsAppRegisterable.
                    new(VipTypeId, "VIP", "شخصية مهمة", "#000000", "Visitor",
                        "None", true, true, true),
                }));

    private IRenderedComponent<BadgeBatchesPage> RenderWithBatches()
    {
        // Grant ViewBatches (page load) + ManageBatches (the destructive row actions,
        // split out of ViewBatches by the 2026-07-24 security review) so the
        // AuthorizedAction-wrapped re-email / revoke actions (incl. their SimfIcons)
        // actually render. The live check found an invalid icon name ("ban") only
        // crashes the circuit once a populated row shows its actions — which the
        // default (no-policy) auth context had hidden.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ViewBatches),
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ManageBatches));
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("badge-batches/list"))
            .SetResult(ApiResult<GridPage<AdminBadgeBatchSummary>>.Ok(
                GridPage<AdminBadgeBatchSummary>.Of(
                    new List<AdminBadgeBatchSummary>
                    {
                        new(ActiveId, "VIP × 3", 3, false, "org@simf.test", SimfClock.Now, true),
                        new(RevokedId, "Normal × 2", 2, true, null, SimfClock.Now, false),
                    },
                    total: 2, skip: 0, top: 20)));
        return RenderComponent<BadgeBatchesPage>();
    }

    // The Contents column used to render BadgeBatch.CountsSummary, ONE English
    // string built server-side, so an otherwise-Arabic page showed "Normal × 4 +
    // VIP × 3". The server now derives the breakdown carrying both names and the
    // page composes it in the language being read.

    private static AdminBadgeBatchSummary OrderWithTiers() =>
        new(ActiveId, "Normal × 4 + VIP × 3", 7, false, null, SimfClock.Now, true,
            "Ministry of Interior Team", "فريق وزارة الداخلية",
            new List<AdminBadgeBatchTier>
            {
                new("Normal", "عادي", 4),
                new("VIP", "كبار الشخصيات", 3),
            });

    private IRenderedComponent<BadgeBatchesPage> RenderOrder(AdminBadgeBatchSummary row)
    {
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ViewBatches));
        JSInterop.Mode = JSRuntimeMode.Loose;
        StubProfileTypes();
        JSInterop.Setup<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("badge-batches/list"))
            .SetResult(ApiResult<GridPage<AdminBadgeBatchSummary>>.Ok(
                GridPage<AdminBadgeBatchSummary>.Of(
                    new List<AdminBadgeBatchSummary> { row }, total: 1, skip: 0, top: 20)));
        return RenderComponent<BadgeBatchesPage>();
    }

    [Fact]
    public void The_contents_column_reads_in_Arabic_under_an_Arabic_culture()
    {
        using var _ = new CultureScope("ar");

        var cut = RenderOrder(OrderWithTiers());

        Assert.Contains("عادي × 4", cut.Markup);
        Assert.Contains("كبار الشخصيات × 3", cut.Markup);
        // The English tier names must be gone, not merely accompanied.
        Assert.DoesNotContain("Normal ×", cut.Markup);
        Assert.DoesNotContain("VIP ×", cut.Markup);
    }

    [Fact]
    public void The_contents_column_reads_in_English_under_an_English_culture()
    {
        using var _ = new CultureScope("en");

        var cut = RenderOrder(OrderWithTiers());

        Assert.Contains("Normal × 4", cut.Markup);
        Assert.Contains("VIP × 3", cut.Markup);
    }

    [Fact]
    public void An_order_with_no_breakdown_falls_back_to_the_stored_summary()
    {
        // Not the direct-registration row: an ordinary order whose members are gone
        // must still render its stored summary rather than going blank.
        using var _ = new CultureScope("ar");

        var cut = RenderOrder(new(
            ActiveId, "Normal × 4 + VIP × 3", 7, false, null, SimfClock.Now, true,
            "Emptied order", "طلب مُفرَّغ", Tiers: null));

        Assert.Contains("Normal × 4 + VIP × 3", cut.Markup);
    }

    [Fact]
    public void The_direct_registration_row_uses_a_localised_label_not_the_stored_prose()
    {
        // It is not a badge order, so it carries no tiers and its stored summary is
        // the English literal "Direct registration". Echoing that left one English
        // cell in an otherwise Arabic table.
        using var _ = new CultureScope("ar");

        var cut = RenderOrder(new(
            ActiveId, "Direct registration", 0, false, null, SimfClock.Now, true,
            "Direct registration", "التسجيل المباشر",
            Tiers: null, IsDirectRegistration: true));

        Assert.Contains("Admin.BadgeBatches.DirectRegistration", cut.Markup);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _previous = CultureInfo.CurrentUICulture;

        public CultureScope(string name) =>
            CultureInfo.CurrentUICulture = new CultureInfo(name);

        public void Dispose() => CultureInfo.CurrentUICulture = _previous;
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

    // ---- Top-up -----------------------------------------------------------
    // The API endpoint shipped ahead of any way to reach it: nothing in the
    // Control Panel called /badge-batches/top-up, so "an order can be topped up"
    // was true only for someone holding an HTTP client. These pin the wiring.

    private IRenderedComponent<BadgeBatchesPage> RenderForTopUp()
    {
        // BulkGenerate, not ManageBatches — top-up MINTS, so it carries the
        // permission its endpoint checks.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ViewBatches),
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.BulkGenerate));
        JSInterop.Mode = JSRuntimeMode.Loose;
        StubProfileTypes();
        JSInterop.Setup<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("badge-batches/list"))
            .SetResult(ApiResult<GridPage<AdminBadgeBatchSummary>>.Ok(
                GridPage<AdminBadgeBatchSummary>.Of(
                    new List<AdminBadgeBatchSummary>
                    {
                        new(ActiveId, "VIP × 3", 3, false, "org@simf.test", SimfClock.Now,
                            true, "Ministry of Interior Team", "فريق وزارة الداخلية"),
                    },
                    total: 1, skip: 0, top: 20)));
        return RenderComponent<BadgeBatchesPage>();
    }

    // RowActions render before the grid's own row-end buttons, and top-up is
    // first in that block, so index 0 is it.
    private static void OpenTheTopUpDialog(IRenderedComponent<BadgeBatchesPage> cut) =>
        cut.FindAll(".simf-grid__td--actions button")[0].Click();

    [Fact]
    public void The_top_up_action_renders_for_an_active_order()
    {
        var cut = RenderForTopUp();

        Assert.Contains("Admin.BadgeBatches.TopUp", cut.Markup);
    }

    [Fact]
    public void The_dialog_shows_what_the_order_already_holds()
    {
        var cut = RenderForTopUp();

        OpenTheTopUpDialog(cut);

        // "Add 3" is only meaningful next to what is already there.
        Assert.Contains("Admin.BadgeBatches.TopUp.Title", cut.Markup);
        Assert.Contains("Ministry of Interior Team", cut.Markup);
        Assert.Contains("VIP × 3", cut.Markup);
    }

    [Fact]
    public void A_top_up_with_no_type_chosen_posts_nothing()
    {
        var cut = RenderForTopUp();
        OpenTheTopUpDialog(cut);

        cut.FindAll(".simf-modal__footer button").Last().Click();

        Assert.Contains("Admin.BadgeBatches.TopUp.TypeRequired", cut.Markup);
        Assert.DoesNotContain(
            JSInterop.Invocations,
            i => i.Identifier == "simfAccount.postJson"
                && ((string)i.Arguments[0]!).Contains("top-up"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("abc")]
    public void A_top_up_with_a_bad_count_posts_nothing(string count)
    {
        var cut = RenderForTopUp();
        OpenTheTopUpDialog(cut);

        cut.Find(".simf-modal select").Change(VipTypeId.ToString());
        cut.Find(".simf-modal input").Input(count);
        cut.FindAll(".simf-modal__footer button").Last().Click();

        Assert.Contains("Admin.BadgeBatches.TopUp.CountInvalid", cut.Markup);
        Assert.DoesNotContain(
            JSInterop.Invocations,
            i => i.Identifier == "simfAccount.postJson"
                && ((string)i.Arguments[0]!).Contains("top-up"));
    }

    [Fact]
    public void Confirming_posts_the_order_the_tier_and_the_count()
    {
        var cut = RenderForTopUp();
        var handler = JSInterop.Setup<ApiResult<AdminTopUpBadgeBatchResponse>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("top-up"))
            .SetResult(ApiResult<AdminTopUpBadgeBatchResponse>.Ok(
                new AdminTopUpBadgeBatchResponse(3, 6)));

        OpenTheTopUpDialog(cut);
        cut.Find(".simf-modal select").Change(VipTypeId.ToString());
        cut.Find(".simf-modal input").Input("3");
        cut.FindAll(".simf-modal__footer button").Last().Click();

        var post = handler.Invocations.Single();
        Assert.Equal(
            "/account/api/admin/visitors/badge-batches/top-up", (string)post.Arguments[0]!);
        var body = Assert.IsType<AdminTopUpBadgeBatchRequest>(post.Arguments[1]);
        Assert.Equal(ActiveId, body.BatchId);
        var line = Assert.Single(body.Batches);
        Assert.Equal(VipTypeId, line.ProfileTypeId);
        Assert.Equal(3, line.Count);
        Assert.Contains("Admin.BadgeBatches.TopUp.Done", cut.Markup);
    }

    [Fact]
    public void A_refusal_keeps_the_dialog_open_with_the_reason_in_it()
    {
        var cut = RenderForTopUp();
        JSInterop.Setup<ApiResult<AdminTopUpBadgeBatchResponse>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("top-up"))
            .SetResult(ApiResult<AdminTopUpBadgeBatchResponse>.Fail(
                new ApiError
                {
                    Code = "VALIDATION_FAILED",
                    Message = "That order has been revoked.",
                    MessageArabic = "تم إلغاء هذا الطلب.",
                }));

        OpenTheTopUpDialog(cut);
        cut.Find(".simf-modal select").Change(VipTypeId.ToString());
        cut.Find(".simf-modal input").Input("3");
        cut.FindAll(".simf-modal__footer button").Last().Click();

        // The server refuses a revoked order and the direct-registration order.
        // Closing the dialog would hide the reason from the person who asked.
        Assert.Contains("Admin.BadgeBatches.TopUp.Title", cut.Markup);
        Assert.Contains("That order has been revoked.", cut.Markup);
    }

    [Fact]
    public void A_failed_tier_lookup_says_so_instead_of_offering_an_empty_picker()
    {
        // The tier lookup is gated on ProfileTypes.View, a different code from
        // the Visitors.BulkGenerate that renders the button, so it can be refused
        // for someone who may legitimately open this dialog. Without this, the
        // picker is empty and the only response to "Add and mint" is "choose a
        // profile type" — with nothing to choose.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ViewBatches),
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.BulkGenerate));
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
                "simfAccount.getJson",
                inv => ((string)inv.Arguments[0]!).Contains("profile-types"))
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Fail(
                new ApiError
                {
                    Code = "FORBIDDEN",
                    Message = "You do not hold ProfileTypes.View.",
                    MessageArabic = "لا تملك صلاحية عرض أنواع الملفات.",
                }));
        JSInterop.Setup<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("badge-batches/list"))
            .SetResult(ApiResult<GridPage<AdminBadgeBatchSummary>>.Ok(
                GridPage<AdminBadgeBatchSummary>.Of(
                    new List<AdminBadgeBatchSummary>
                    {
                        new(ActiveId, "VIP × 3", 3, false, null, SimfClock.Now, true, "Order", "طلب"),
                    },
                    total: 1, skip: 0, top: 20)));

        var cut = RenderComponent<BadgeBatchesPage>();
        OpenTheTopUpDialog(cut);

        Assert.Contains("You do not hold ProfileTypes.View.", cut.Markup);
        // And the confirm button cannot be pressed into a dead end.
        var confirm = cut.FindAll(".simf-modal__footer button").Last();
        Assert.True(confirm.HasAttribute("disabled"));
    }

    [Fact]
    public void Without_BulkGenerate_the_top_up_action_is_not_rendered()
    {
        // ManageBatches alone must not reach it: re-emailing or revoking an order
        // is a different authority from creating more of it. A hidden button over
        // an open API would be theatre, so the endpoint is gated too — see
        // PermissionEnforcementTests.
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ViewBatches),
            PermissionCatalog.PolicyFor(PermissionCatalog.Visitors.ManageBatches));
        JSInterop.Mode = JSRuntimeMode.Loose;
        StubProfileTypes();
        JSInterop.Setup<ApiResult<GridPage<AdminBadgeBatchSummary>>>(
                "simfAccount.postJson",
                inv => ((string)inv.Arguments[0]!).Contains("badge-batches/list"))
            .SetResult(ApiResult<GridPage<AdminBadgeBatchSummary>>.Ok(
                GridPage<AdminBadgeBatchSummary>.Of(
                    new List<AdminBadgeBatchSummary>
                    {
                        new(ActiveId, "VIP × 3", 3, false, null, SimfClock.Now, true),
                    },
                    total: 1, skip: 0, top: 20)));

        var cut = RenderComponent<BadgeBatchesPage>();

        Assert.DoesNotContain("Admin.BadgeBatches.TopUp", cut.Markup);
        // The neighbouring actions it does hold are still there.
        Assert.Contains("Admin.BadgeBatches.Revoke", cut.Markup);
    }
}
