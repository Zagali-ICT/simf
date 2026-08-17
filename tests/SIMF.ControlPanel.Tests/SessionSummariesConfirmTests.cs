// A19 (2026-07-26) — the session-summary editor must WARN before a save that
// would withdraw the محضر. Saving an edited summary clears its approval and, if
// it was live, unpublishes it (server side: D-472 + owner 2026-07-19), which
// used to happen silently on any Save click. These tests lock the three
// behaviours: an edited published summary is gated behind SimfConfirm; cancelling
// fires no PUT; a save that changed nothing is NOT gated.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class SessionSummariesConfirmTests : CpComponentTestBase
{
    private static readonly Guid SessionId =
        Guid.Parse("11111111-2222-3333-4444-555555555555");

    private const string ListUrl = "/account/api/admin/session-summaries";

    // The desk is a server-paged grid: it POSTs a GridQuery to .../list rather than
    // GETting the whole set, so the mock has to match the verb as well as the route.
    private const string ListPostUrl = "/account/api/admin/session-summaries/list";
    private static string DetailUrl => $"{ListUrl}/{SessionId}";

    private static AdminSessionSummaryRow Row(bool isPublished, bool isApproved) => new(
        SessionId, "SES-001", "Opening session", "الجلسة الافتتاحية",
        DateTime.UnixEpoch,
        HasSummary: true,
        GeneratedByAi: false,
        IsPublished: isPublished,
        PublishedAt: isPublished ? DateTime.UnixEpoch : null,
        UpdatedAt: DateTime.UnixEpoch,
        IsInReview: false,
        IsApproved: isApproved,
        ApprovedAt: isApproved ? DateTime.UnixEpoch : null);

    private static AdminSessionSummaryDetail Detail(bool isPublished, bool isApproved) => new(
        SessionId, "SES-001", "Opening session", "الجلسة الافتتاحية",
        KeyPoints: "Point one",
        KeyPointsArabic: string.Empty,
        Recommendations: string.Empty,
        RecommendationsArabic: string.Empty,
        Speakers: string.Empty,
        SpeakersArabic: string.Empty,
        FullText: "Minutes.",
        FullTextArabic: "محضر.",
        AiModel: null,
        IsPublished: isPublished,
        PublishedAt: isPublished ? DateTime.UnixEpoch : null,
        CreatedAt: DateTime.UnixEpoch,
        UpdatedAt: DateTime.UnixEpoch,
        IsInReview: false,
        IsApproved: isApproved,
        ApprovedAt: isApproved ? DateTime.UnixEpoch : null,
        Subtitle: null,
        SubtitleArabic: null,
        AiDraftFullTextArabic: null,
        AiDraftGeneratedAt: null);

    /// <summary>Renders the desk, opens the editor on the single row, and returns
    /// the component plus the stubbed PUT handler.</summary>
    private (IRenderedComponent<SessionSummariesList> Cut,
        JSRuntimeInvocationHandler<ApiResult<AdminSessionSummaryDetail>> Put) OpenEditor(
        bool isPublished, bool isApproved)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // The row's Edit action sits behind AuthorizedAction (SessionSummaries.Edit).
        Authorization.SetPolicies(
            PermissionCatalog.PolicyFor(PermissionCatalog.SessionSummaries.Edit));
        JSInterop.Setup<ApiResult<GridPage<AdminSessionSummaryRow>>>(
                "simfAccount.postJson",
                inv => inv.Arguments.Count > 0
                    && (inv.Arguments[0] as string) == ListPostUrl)
            .SetResult(ApiResult<GridPage<AdminSessionSummaryRow>>.Ok(
                GridPage<AdminSessionSummaryRow>.Of(
                    new[] { Row(isPublished, isApproved) }, total: 1, skip: 0, top: 20)));
        JSInterop.Setup<ApiResult<AdminSessionSummaryDetail>>(
                "simfAccount.getJson", DetailUrl)
            .SetResult(ApiResult<AdminSessionSummaryDetail>.Ok(Detail(isPublished, isApproved)));
        var put = JSInterop.Setup<ApiResult<AdminSessionSummaryDetail>>(
                "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminSessionSummaryDetail>.Ok(Detail(isPublished, isApproved)));

        var cut = RenderComponent<SessionSummariesList>();
        cut.WaitForAssertion(() =>
            Assert.Contains("Admin.SessionSummaries.Action.Edit", cut.Markup));
        cut.Find("[title='Admin.SessionSummaries.Action.Edit']").Click();
        cut.WaitForState(() => cut.FindAll("textarea").Count > 0);
        return (cut, put);
    }

    [Fact]
    public void A19_editing_a_published_summary_gates_the_save_behind_a_warning()
    {
        var (cut, put) = OpenEditor(isPublished: true, isApproved: true);

        // The reviewer fixes a typo in the first editable section...
        cut.Find("textarea").Input("Point one (typo fixed)");
        cut.Find(".simf-modal__footer .simf-button").Click();

        // ...and is told what saving costs BEFORE anything is sent.
        Assert.Contains("Admin.SessionSummaries.Confirm.UnpublishOnSave", cut.Markup);
        Assert.Empty(put.Invocations);

        // Confirming goes through to the same PUT as before.
        cut.Find(".simf-confirm__message");
        cut.FindAll(".simf-modal__footer .simf-button--danger")[^1].Click();
        var call = Assert.Single(put.Invocations);
        Assert.Equal(DetailUrl, (string)call.Arguments[0]!);
    }

    [Fact]
    public void A19_cancelling_the_warning_sends_no_save()
    {
        var (cut, put) = OpenEditor(isPublished: true, isApproved: true);

        cut.Find("textarea").Input("Point one (typo fixed)");
        cut.Find(".simf-modal__footer .simf-button").Click();
        Assert.Contains("Admin.SessionSummaries.Confirm.UnpublishOnSave", cut.Markup);

        cut.FindAll(".simf-modal__footer .simf-button--secondary")[^1].Click();

        Assert.DoesNotContain("Admin.SessionSummaries.Confirm.UnpublishOnSave", cut.Markup);
        Assert.Empty(put.Invocations);
    }

    [Fact]
    public void A19_a_save_that_changed_nothing_is_not_gated()
    {
        var (cut, put) = OpenEditor(isPublished: true, isApproved: true);

        // Straight to Save, nothing edited — no warning, and the server-side rule
        // leaves the summary published.
        cut.Find(".simf-modal__footer .simf-button").Click();

        Assert.DoesNotContain("Admin.SessionSummaries.Confirm.UnpublishOnSave", cut.Markup);
        Assert.Single(put.Invocations);
    }

    [Fact]
    public void A19_editing_a_draft_summary_is_not_gated()
    {
        var (cut, put) = OpenEditor(isPublished: false, isApproved: false);

        // A never-approved draft loses nothing on save, so it saves straight away.
        cut.Find("textarea").Input("Point one (typo fixed)");
        cut.Find(".simf-modal__footer .simf-button").Click();

        Assert.DoesNotContain("Admin.SessionSummaries.Confirm.", cut.Markup);
        Assert.Single(put.Invocations);
    }
}
