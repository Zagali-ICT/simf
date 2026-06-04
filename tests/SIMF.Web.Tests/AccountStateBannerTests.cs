// D-194 — bUnit coverage for the P11/D-052 account-state banner pages
// (PendingApproval + Rejected). These pages read the account_state +
// rejection_reason cookie claims, redirect on the off-state, and
// render culture-aware messaging. They had zero coverage before D-194.
using System.Globalization;
using System.Security.Claims;
using Bunit;
using SIMF.Web.Components.Pages.Account;

namespace SIMF.Web.Tests;

public sealed class AccountStateBannerTests : WebComponentTestBase
{
    // -- PendingApproval -------------------------------------------------------

    [Fact]
    public void Pending_with_PendingApproval_state_renders_the_account_email()
    {
        Authorization.SetClaims(
            new Claim("account_state", "PendingApproval"),
            new Claim(ClaimTypes.Email, "pending.visitor@simf.test"));

        var cut = RenderComponent<PendingApproval>();

        // The email shows under the supporting copy; no redirect.
        Assert.Contains("pending.visitor@simf.test", cut.Markup);
        Assert.DoesNotContain("/account/profile", Navigation.Uri);
        Assert.DoesNotContain("/account/rejected", Navigation.Uri);
    }

    [Fact]
    public void Pending_with_Approved_state_redirects_to_profile()
    {
        Authorization.SetClaims(new Claim("account_state", "Approved"));

        RenderComponent<PendingApproval>();

        Assert.EndsWith("/account/profile", Navigation.Uri);
    }

    [Fact]
    public void Pending_with_Rejected_state_redirects_to_rejected()
    {
        Authorization.SetClaims(new Claim("account_state", "Rejected"));

        RenderComponent<PendingApproval>();

        Assert.EndsWith("/account/rejected", Navigation.Uri);
    }

    // -- Rejected --------------------------------------------------------------

    [Fact]
    public void Rejected_with_reason_renders_the_reason_in_an_error_alert()
    {
        Authorization.SetClaims(
            new Claim("account_state", "Rejected"),
            new Claim("rejection_reason", "Your ID document was unreadable."));

        var cut = RenderComponent<Rejected>();

        Assert.Contains("Your ID document was unreadable.", cut.Markup);
        Assert.Contains("simf-alert--error", cut.Markup);
    }

    [Fact]
    public void Rejected_without_reason_renders_the_no_reason_alert()
    {
        Authorization.SetClaims(new Claim("account_state", "Rejected"));

        var cut = RenderComponent<Rejected>();

        // No reason claim → the "no reason provided" alert + key.
        // D-194 finding: the page requests `<SimfAlert Variant="warning">`
        // but SimfAlert only maps error / success / (info fallback) — so
        // the no-reason case currently degrades to `simf-alert--info`,
        // NOT a visually-distinct warning. This test pins the ACTUAL
        // current behaviour; the variant gap is flagged for the review
        // pass (see DECISIONS_LOG D-194).
        Assert.Contains("Account.Rejected.NoReasonProvided", cut.Markup);
        Assert.Contains("simf-alert", cut.Markup);
        Assert.Contains("simf-alert--info", cut.Markup);
    }

    [Fact]
    public void Rejected_in_Arabic_culture_prefers_the_arabic_reason()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            Authorization.SetClaims(
                new Claim("account_state", "Rejected"),
                new Claim("rejection_reason", "English reason."),
                new Claim("rejection_reason_ar", "السبب بالعربية."));

            var cut = RenderComponent<Rejected>();

            Assert.Contains("السبب بالعربية.", cut.Markup);
            Assert.DoesNotContain("English reason.", cut.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Rejected_in_Arabic_falls_back_to_english_when_arabic_reason_absent()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = new CultureInfo("ar");
            Authorization.SetClaims(
                new Claim("account_state", "Rejected"),
                new Claim("rejection_reason", "Only English on file."));

            var cut = RenderComponent<Rejected>();

            Assert.Contains("Only English on file.", cut.Markup);
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    [Fact]
    public void Rejected_with_Approved_state_redirects_to_profile()
    {
        Authorization.SetClaims(new Claim("account_state", "Approved"));

        RenderComponent<Rejected>();

        Assert.EndsWith("/account/profile", Navigation.Uri);
    }

    [Fact]
    public void Rejected_with_PendingApproval_state_redirects_to_pending()
    {
        Authorization.SetClaims(new Claim("account_state", "PendingApproval"));

        RenderComponent<Rejected>();

        Assert.EndsWith("/account/pending", Navigation.Uri);
    }
}
