using SIMF.Application.Email;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// #8 (owner) — the shared bilingual transactional-email builder. Pure unit
/// tests (no host / DB): a code email carries BOTH the English and the Arabic
/// block + the code + a per-language expiry, and a code-less notice carries both
/// blocks.
/// </summary>
public sealed class TransactionalEmailTests
{
    [Fact]
    public void Code_renders_both_languages_the_code_and_the_expiry()
    {
        var message = TransactionalEmail.Code(
            "u@simf.test",
            "SIMF sign-in code",
            enLead: "Your SIMF sign-in code is",
            arLead: "رمز تسجيل الدخول الخاص بك هو",
            code: "123456",
            expiryMinutes: 10);

        Assert.Equal("u@simf.test", message.To);
        Assert.Equal("SIMF sign-in code", message.Subject);
        // English block: lead + code + expiry.
        Assert.Contains(
            "Your SIMF sign-in code is <strong>123456</strong>", message.HtmlBody);
        Assert.Contains("The code expires in 10 minutes.", message.HtmlBody);
        // Arabic block (right-to-left): lead + code + Arabic expiry.
        Assert.Contains(
            "رمز تسجيل الدخول الخاص بك هو <strong>123456</strong>", message.HtmlBody);
        Assert.Contains("ينتهي الرمز خلال 10 دقائق.", message.HtmlBody);
        Assert.Contains("dir=\"rtl\"", message.HtmlBody);
    }

    [Fact]
    public void Code_appends_the_optional_bilingual_note()
    {
        var message = TransactionalEmail.Code(
            "u@simf.test",
            "SIMF password reset",
            enLead: "Your SIMF password reset code is",
            arLead: "رمز إعادة تعيين كلمة المرور الخاص بك هو",
            code: "654321",
            expiryMinutes: 15,
            enNote: "If you did not request a password reset, you can ignore this email.",
            arNote: "إذا لم تطلب إعادة تعيين كلمة المرور فيمكنك تجاهل هذه الرسالة.");

        Assert.Contains("If you did not request a password reset", message.HtmlBody);
        Assert.Contains("إذا لم تطلب إعادة تعيين كلمة المرور", message.HtmlBody);
    }

    [Fact]
    public void Code_omits_the_note_cleanly_when_none_is_given()
    {
        var message = TransactionalEmail.Code(
            "u@simf.test", "SIMF sign-in code",
            enLead: "x", arLead: "س", code: "111111", expiryMinutes: 10);

        // The expiry line ends the paragraph — no dangling space / trailing note.
        Assert.Contains("The code expires in 10 minutes.</p>", message.HtmlBody);
        Assert.Contains("ينتهي الرمز خلال 10 دقائق.</p>", message.HtmlBody);
    }

    [Fact]
    public void Notice_renders_both_language_blocks()
    {
        var message = TransactionalEmail.Notice(
            "u@simf.test",
            "SIMF account already exists",
            enHtml: "<p>An account already exists.</p>",
            arHtml: "<p>يوجد حساب مسجَّل بالفعل.</p>");

        Assert.Contains("<p>An account already exists.</p>", message.HtmlBody);
        Assert.Contains("<p>يوجد حساب مسجَّل بالفعل.</p>", message.HtmlBody);
        Assert.Contains("dir=\"rtl\"", message.HtmlBody);
    }
}
