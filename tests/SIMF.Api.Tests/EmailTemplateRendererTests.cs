// D-735 — pure unit tests for the transactional-email template rendering
// primitives: the {Token} substitutor / bilingual composer / unknown-token
// finder (SIMF.Application.Email.EmailTemplateRenderer) and the code-owned
// default catalogue (EmailTemplateCatalog). No host, no database.
using SIMF.Application.Email;
using SIMF.Common.Enums;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class EmailTemplateRendererTests
{
    // -- Render --------------------------------------------------------------

    [Fact]
    public void Render_substitutes_the_token_and_html_encodes_the_value()
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "a<b>",
        };

        var rendered = EmailTemplateRenderer.Render("Your code is {Code}.", tokens);

        // The value is HTML-encoded — the template author writes HTML, the
        // substituted runtime value is untrusted.
        Assert.Equal("Your code is a&lt;b&gt;.", rendered);
    }

    [Fact]
    public void Render_leaves_an_unknown_token_literal()
    {
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "123456",
        };

        var rendered = EmailTemplateRenderer.Render("Hello {Foo} world.", tokens);

        Assert.Equal("Hello {Foo} world.", rendered);
    }

    [Fact]
    public void Render_resolves_tokens_case_insensitively()
    {
        // A case-insensitive dictionary resolves {code} and {Code} alike.
        var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = "123456",
        };

        var rendered = EmailTemplateRenderer.Render("{code} and {Code}", tokens);

        Assert.Equal("123456 and 123456", rendered);
    }

    // -- ComposeBody ---------------------------------------------------------

    [Fact]
    public void ComposeBody_joins_english_then_rtl_arabic_block()
    {
        var composed = EmailTemplateRenderer.ComposeBody("EN-BODY", "AR-BODY");

        Assert.Equal("EN-BODY<hr/><div dir=\"rtl\">AR-BODY</div>", composed);
        Assert.Contains("dir=\"rtl\"", composed, StringComparison.Ordinal);
        Assert.Contains("EN-BODY", composed, StringComparison.Ordinal);
        Assert.Contains("AR-BODY", composed, StringComparison.Ordinal);
    }

    // -- FindUnknownTokens ---------------------------------------------------

    [Fact]
    public void FindUnknownTokens_is_empty_when_every_token_is_known()
    {
        var known = EmailTemplateCatalog.KnownTokenNames(EmailTemplateType.SignInOtp);

        var unknown = EmailTemplateRenderer.FindUnknownTokens(
            "<p>{Code} expires in {ExpiryMinutes} minutes.</p>", known);

        Assert.Empty(unknown);
    }

    [Fact]
    public void FindUnknownTokens_surfaces_a_token_the_template_does_not_define()
    {
        var known = EmailTemplateCatalog.KnownTokenNames(EmailTemplateType.SignInOtp);

        var unknown = EmailTemplateRenderer.FindUnknownTokens("<p>{Foo}</p>", known);

        var one = Assert.Single(unknown);
        Assert.Equal("Foo", one);
    }

    // -- EmailTemplateCatalog ------------------------------------------------

    [Fact]
    public void Catalog_default_signin_otp_has_the_expected_subject_body_and_tokens()
    {
        var def = EmailTemplateCatalog.Default(EmailTemplateType.SignInOtp);

        Assert.Equal("SIMF sign-in code", def.Subject);
        Assert.Contains("{Code}", def.BodyEn, StringComparison.Ordinal);
        Assert.Contains("{ExpiryMinutes}", def.BodyEn, StringComparison.Ordinal);
        Assert.Equal(2, def.Tokens.Count);
    }

    [Fact]
    public void Catalog_all_lists_the_ten_transactional_templates()
    {
        // The count has been wrong more often than right: it sat stale at 6 on
        // the base branch through D-751 (BulkBadgeDelivery), #24
        // (EmailChangeVerification + EmailChangedNotice) and BUG-024
        // (ExhibitorLeadCapture). It reached 11 with the account-deletion code,
        // then back to 10 when EmailChangeVerification's definition was dropped
        // — G1 removed the feature in 2026-07, and nothing could send it after.
        Assert.Equal(10, EmailTemplateCatalog.All.Count);
    }

    [Fact]
    public void Catalog_default_exhibitor_lead_capture_carries_the_bilingual_lead_tokens()
    {
        // BUG-024 — the booth lead card emailed to the exhibitor. Each displayed
        // field is a bilingual pair so the EN block renders the English value and
        // the AR block the Arabic one; the scan time is a single shared token.
        var def = EmailTemplateCatalog.Default(EmailTemplateType.ExhibitorLeadCapture);

        Assert.Contains("{VisitorName}", def.BodyEn, StringComparison.Ordinal);
        Assert.Contains("{VisitorNameArabic}", def.BodyAr, StringComparison.Ordinal);
        Assert.Contains("{ScannedAt}", def.BodyEn, StringComparison.Ordinal);
        Assert.Contains("{ScannedAt}", def.BodyAr, StringComparison.Ordinal);
        Assert.Contains("{Note}", def.BodyEn, StringComparison.Ordinal);
        Assert.DoesNotContain("{Code}", def.BodyEn, StringComparison.Ordinal);
        Assert.Equal(8, def.Tokens.Count);
    }

    [Fact]
    public void Catalog_no_longer_carries_email_change_verification()
    {
        // G1 (owner, 2026-07-30) deleted self-service email change — the screen,
        // both endpoints and EmailChangeService — so nothing has been able to
        // send this since. Its definition survived as a dead row on the admin
        // grid, inviting someone to reword an email that is never sent.
        Assert.False(
            EmailTemplateCatalog.IsCatalogued(EmailTemplateType.EmailChangeVerification));
        Assert.DoesNotContain(
            EmailTemplateCatalog.All,
            d => d.Type == EmailTemplateType.EmailChangeVerification);

        // The ENUM value stays: it is frozen against removal, and
        // AccountCodePurpose.EmailChangeVerification is still persisted by name
        // on historical AccountCode rows.
        Assert.Equal(7, (int)EmailTemplateType.EmailChangeVerification);
    }

    [Fact]
    public void Catalog_default_throws_for_an_uncatalogued_type_rather_than_guessing()
    {
        // The enum is a SUPERSET of the catalogue and always will be. Default
        // must not quietly hand back some other template's copy for a type it
        // does not have — DefaultOrFallback is the never-throw variant, and it
        // exists for the resolver alone.
        Assert.Throws<KeyNotFoundException>(
            () => EmailTemplateCatalog.Default(EmailTemplateType.EmailChangeVerification));
    }

    [Fact]
    public void Catalog_default_email_changed_notice_carries_the_new_email_token()
    {
        var def = EmailTemplateCatalog.Default(EmailTemplateType.EmailChangedNotice);

        Assert.Equal("SIMF login email changed", def.Subject);
        Assert.Contains("{NewEmail}", def.BodyEn, StringComparison.Ordinal);
        Assert.DoesNotContain("{Code}", def.BodyEn, StringComparison.Ordinal);
        var token = Assert.Single(def.Tokens);
        Assert.Equal("NewEmail", token.Name);
    }

    [Fact]
    public void Catalog_account_exists_default_defines_no_tokens()
    {
        var def = EmailTemplateCatalog.Default(EmailTemplateType.AccountExists);

        Assert.Empty(def.Tokens);
    }
}
