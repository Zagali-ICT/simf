// D-728 (owner item 9) — bUnit tests for the shared ChangeAccountTypeBlock used
// to flip an account between the audience (Visitor) and partner (Other) scope
// from the Others / Visitors detail views. Verifies the target list is filtered
// to the OPPOSITE scope's active types, and that a change POSTs the flip.
using Bunit;
using Microsoft.AspNetCore.Components;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.ControlPanel.Components.Pages.Admin;
using Xunit;

namespace SIMF.ControlPanel.Tests;

public sealed class ChangeAccountTypeBlockTests : CpComponentTestBase
{
    private static AdminProfileTypeSummary Type(
        string name, bool isVisitor, bool isActive) =>
        new(Guid.NewGuid(), name, name, "#244A77", "Visitor", "None",
            isActive, isVisitor, IsAppRegisterable: isVisitor);

    [Fact]
    public void Offers_only_the_opposite_scope_active_types()
    {
        var visitorType = Type("VIP", isVisitor: true, isActive: true);
        var partnerType = Type("Sponsor", isVisitor: false, isActive: true);
        var inactivePartner = Type("Old Sponsor", isVisitor: false, isActive: false);

        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                new List<AdminProfileTypeSummary>
                {
                    visitorType, partnerType, inactivePartner,
                }));

        // A visitor-scope account → the flip targets the partner scope only.
        var cut = RenderComponent<ChangeAccountTypeBlock>(p => p
            .Add(c => c.AccountId, Guid.NewGuid())
            .Add(c => c.IsVisitorScope, true));

        var options = cut.FindAll("option");
        Assert.Contains(options, o => o.TextContent.Contains("Sponsor"));
        Assert.DoesNotContain(options, o => o.TextContent.Contains("VIP"));
        Assert.DoesNotContain(options, o => o.TextContent.Contains("Old Sponsor"));
    }

    [Fact]
    public void Change_posts_the_flip_and_signals_the_host()
    {
        var partnerType = Type("Sponsor", isVisitor: false, isActive: true);
        var accountId = Guid.NewGuid();

        JSInterop.Setup<ApiResult<IReadOnlyList<AdminProfileTypeSummary>>>(
            "simfAccount.getJson", _ => true)
            .SetResult(ApiResult<IReadOnlyList<AdminProfileTypeSummary>>.Ok(
                new List<AdminProfileTypeSummary> { partnerType }));
        JSInterop.Setup<ApiResult<bool>>("simfAccount.postJson", _ => true)
            .SetResult(ApiResult<bool>.Ok(true));
        // D-809 — the flip now opens a SimfConfirm, and SimfModal binds its
        // focus trap through JS on open.
        JSInterop.SetupVoid("simfModal.bind", _ => true).SetVoidResult();

        var changed = false;
        var cut = RenderComponent<ChangeAccountTypeBlock>(p => p
            .Add(c => c.AccountId, accountId)
            .Add(c => c.IsVisitorScope, true)
            .Add(c => c.OnChanged,
                EventCallback.Factory.Create(this, () => changed = true)));

        // Pick the partner type, then trigger the flip. D-809 — the action button
        // stages a confirmation; nothing is posted until the admin accepts it.
        cut.Find("select").Change(partnerType.Id.ToString());
        cut.FindAll("button")
           .First(b => b.TextContent.Contains("Admin.ChangeType.Action"))
           .Click();

        Assert.Contains("Admin.ChangeType.Confirm.Title", cut.Markup);
        Assert.Empty(JSInterop.Invocations["simfAccount.postJson"]);

        // Structural selector, not the label — a ConfirmLabel edit must not
        // masquerade as a broken guard.
        cut.FindAll(".simf-modal__footer .simf-button--primary")[^1].Click();

        var invocation = JSInterop.VerifyInvoke("simfAccount.postJson");
        Assert.Equal(
            $"/account/api/admin/accounts/{accountId}/change-type",
            invocation.Arguments[0]);
        Assert.True(changed);
        // The success alert replaces the picker.
        Assert.Contains("Admin.ChangeType.Success", cut.Markup);
    }
}
