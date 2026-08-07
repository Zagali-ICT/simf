// D-353 — behaviour tests for InterestAddEdit (the merged Add/Edit form).
// Pins the IsEdit branch: Create POSTs, Edit PUTs, and the Active checkbox
// only appears in Edit mode. Mirrors the ProfileTypeForm harness pattern.
using Bunit;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.ControlPanel.Components.Pages.Admin;

namespace SIMF.ControlPanel.Tests;

public sealed class InterestAddEditTests : CpComponentTestBase
{
    [Fact]
    public void Add_mode_hides_the_Active_checkbox()
    {
        var cut = RenderComponent<InterestAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // The IsActive checkbox label only renders in Edit mode.
        Assert.DoesNotContain("Admin.Interests.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Edit_mode_shows_the_Active_checkbox()
    {
        var row = new AdminInterestSummary(
            Guid.NewGuid(), "Naval", "بحرية", 10, IsActive: true, DateTime.UnixEpoch);

        var cut = RenderComponent<InterestAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, true)
            .Add(p => p.Initial, row));

        Assert.Contains("Admin.Interests.Field.IsActive", cut.Markup);
    }

    [Fact]
    public void Add_mode_posts_a_create_request()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var created = new AdminInterestSummary(
            Guid.NewGuid(), "Naval Engineering", "الهندسة البحرية", 10, true, DateTime.UnixEpoch);
        var handler = JSInterop.Setup<ApiResult<AdminInterestSummary>>(
            "simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminInterestSummary>.Ok(created));

        var cut = RenderComponent<InterestAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, false));

        // Re-query after each Change — @bind re-renders and invalidates a cached handle.
        cut.FindAll("input.simf-field__input")[0].Change("Naval Engineering");
        cut.FindAll("input.simf-field__input")[1].Change("الهندسة البحرية");
        cut.Find("form").Submit();

        var posted = handler.Invocations.Single();
        Assert.Equal("/account/api/admin/interests", (string)posted.Arguments[0]!);
        var body = (AdminCreateInterestRequest)posted.Arguments[1]!;
        Assert.Equal("Naval Engineering", body.Name);
        Assert.Equal("الهندسة البحرية", body.NameArabic);
    }

    [Fact]
    public void Server_validation_failure_shows_the_specific_field_reason()
    {
        // Client-side field validation was removed (it duplicated the server
        // FluentValidation). A server VALIDATION_FAILED reason must now surface
        // via DetailedMessageForCurrentCulture — the specific detail, not the
        // generic top-level "one or more fields are invalid" message.
        JSInterop.Mode = JSRuntimeMode.Loose;
        var error = new ApiError
        {
            Code = ErrorCodes.ValidationFailed,
            Message = "One or more fields are invalid.",
            MessageArabic = "يوجد حقل أو أكثر غير صالح.",
            Details =
            [
                new ApiErrorDetail
                {
                    Field = "Name",
                    Message = "The English name is required.",
                    MessageArabic = "الاسم بالإنجليزية مطلوب.",
                },
            ],
        };
        JSInterop.Setup<ApiResult<AdminInterestSummary>>("simfAccount.postJson", _ => true)
            .SetResult(ApiResult<AdminInterestSummary>.Fail(error));

        var cut = RenderComponent<InterestAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, false));

        cut.FindAll("input.simf-field__input")[0].Change("X");
        cut.FindAll("input.simf-field__input")[1].Change("Y");
        cut.Find("form").Submit();

        Assert.Contains("The English name is required.", cut.Markup);
        Assert.DoesNotContain("One or more fields are invalid.", cut.Markup);
    }

    [Fact]
    public void Edit_mode_puts_an_update_request_to_the_row_id()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var row = new AdminInterestSummary(
            Guid.NewGuid(), "Naval", "بحرية", 10, IsActive: true, DateTime.UnixEpoch);
        var handler = JSInterop.Setup<ApiResult<AdminInterestSummary>>(
            "simfAccount.putJson", _ => true)
            .SetResult(ApiResult<AdminInterestSummary>.Ok(row));

        var cut = RenderComponent<InterestAddEdit>(parameters => parameters
            .Add(p => p.IsEdit, true)
            .Add(p => p.Initial, row));

        // Initial pre-fills every field, so submit straight away.
        cut.Find("form").Submit();

        var put = handler.Invocations.Single();
        Assert.Equal($"/account/api/admin/interests/{row.Id}", (string)put.Arguments[0]!);
        Assert.IsType<AdminUpdateInterestRequest>(put.Arguments[1]);
    }
}
