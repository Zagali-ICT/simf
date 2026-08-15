// Behaviour tests for the event-edition page (/admin/editions).
//
// Opening a year clears EVERY attendee's badge, so most of what is pinned here
// is that the action cannot happen by accident: the primary button opens a
// confirmation rather than the year, a malformed year never reaches the dialog,
// and a server refusal leaves the dialog open so the correction is made where
// the mistake was. Run it by accident mid-event and the whole population is
// locked out until every badge is re-issued.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class EventEditionPageTests : CpComponentTestBase
{
    private static AdminEventEditionResponse Edition(int year = 2026, int cleared = 0) =>
        new(year, new DateTime(2026, 1, 1, 8, 0, 0), null, cleared);

    private IRenderedComponent<EventEdition> RenderLoaded(
        AdminEventEditionResponse? edition = null)
    {
        // Editions.Open is what renders the action at all — see the deny-path
        // test below, which grants only View and asserts the button is absent.
        Grant(PermissionCatalog.Editions.View, PermissionCatalog.Editions.Open);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<AdminEventEditionResponse>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<AdminEventEditionResponse>.Ok(edition ?? Edition()));
        return RenderComponent<EventEdition>();
    }

    [Fact]
    public void Shows_the_open_year_and_the_last_reissue_count()
    {
        var cut = RenderLoaded(Edition(2026, cleared: 412));

        Assert.Contains("2026", cut.Markup);
        // The count is the only evidence an operator has that a past re-issue
        // actually ran, so it is on the page rather than only in a toast.
        Assert.Contains("412", cut.Markup);
        Assert.Contains("Admin.Editions.NeverClosed", cut.Markup);
    }

    [Fact]
    public void The_next_year_is_prefilled_but_still_typed()
    {
        var cut = RenderLoaded(Edition(2026));

        var input = cut.Find(".simf-form__fields input");
        Assert.Equal("2027", input.GetAttribute("value"));
    }

    [Fact]
    public void Opening_a_year_asks_for_confirmation_first()
    {
        var cut = RenderLoaded(Edition(2026));

        // Nothing posted yet: the button opens a dialog, never the year.
        cut.Find(".simf-form__actions button").Click();

        Assert.Contains("Admin.Editions.Open.Confirm.Title", cut.Markup);
        Assert.DoesNotContain(
            JSInterop.Invocations, i => i.Identifier == "simfAccount.postJson");
    }

    // "20265" is deliberately absent: the field caps at four characters, so a
    // five-digit year is not a state the page can be in. The server still guards
    // the range - see EventEditionTests - and 1999 exercises that same guard
    // through a value the field CAN hold.
    [Theory]
    [InlineData("202")]
    [InlineData("1999")]
    [InlineData("")]
    [InlineData("abcd")]
    public void A_malformed_year_never_reaches_the_dialog(string year)
    {
        var cut = RenderLoaded(Edition(2026));

        cut.Find(".simf-form__fields input").Input(year);
        cut.Find(".simf-form__actions button").Click();

        // Corrected while the field is still in front of the operator.
        Assert.Contains("Admin.Editions.Open.YearInvalid", cut.Markup);
        Assert.DoesNotContain("Admin.Editions.Open.Confirm.Title", cut.Markup);
    }

    [Fact]
    public void Confirming_posts_the_year_and_reports_how_many_badges_cleared()
    {
        var cut = RenderLoaded(Edition(2026));
        var handler = JSInterop.Setup<ApiResult<AdminOpenEditionResponse>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminOpenEditionResponse>.Ok(
                new AdminOpenEditionResponse(2027, 412)));

        cut.Find(".simf-form__actions button").Click();
        cut.FindAll(".simf-modal__footer button").Last().Click();

        var post = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/editions/open", (string)post.Arguments[0]!);
        var body = Assert.IsType<AdminOpenEditionRequest>(post.Arguments[1]);
        Assert.Equal(2027, body.Year);
        Assert.Contains("Admin.Editions.Open.Done", cut.Markup);
    }

    [Fact]
    public void A_refusal_leaves_the_dialog_open_so_the_year_can_be_corrected()
    {
        var cut = RenderLoaded(Edition(2026));
        JSInterop.Setup<ApiResult<AdminOpenEditionResponse>>(
                "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminOpenEditionResponse>.Fail(
                new ApiError
                {
                    Code = "VALIDATION_FAILED",
                    Message = "That year is already open.",
                    MessageArabic = "هذه السنة مفتوحة بالفعل.",
                }));

        cut.Find(".simf-form__actions button").Click();
        cut.FindAll(".simf-modal__footer button").Last().Click();

        // The server refuses an already-open or earlier year. Closing the dialog
        // would hide the correction from the person making it.
        Assert.Contains("Admin.Editions.Open.Confirm.Title", cut.Markup);
        Assert.Contains("That year is already open.", cut.Markup);
    }

    [Fact]
    public void View_only_cannot_open_a_year()
    {
        // The page gates on Editions.View; the action needs Editions.Open.
        // Without the split, a read-only admin could clear every badge at the
        // event and learn of it from the 403 alone.
        Grant(PermissionCatalog.Editions.View);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<AdminEventEditionResponse>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<AdminEventEditionResponse>.Ok(Edition(2026)));

        var cut = RenderComponent<EventEdition>();

        // The year is still readable.
        Assert.Contains("2026", cut.Markup);
        Assert.Empty(cut.FindAll(".simf-form__actions button"));
    }

    [Fact]
    public void A_failed_load_offers_retry_rather_than_reading_loading_forever()
    {
        Grant(PermissionCatalog.Editions.View, PermissionCatalog.Editions.Open);
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<ApiResult<AdminEventEditionResponse>>(
                "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<AdminEventEditionResponse>.Fail(
                new ApiError
                {
                    Code = "INTERNAL_ERROR",
                    Message = "boom",
                    MessageArabic = "boom",
                }));

        var cut = RenderComponent<EventEdition>();

        Assert.Contains("Common.Retry", cut.Markup);
        Assert.DoesNotContain("Admin.Editions.Loading", cut.Markup);
    }
}
