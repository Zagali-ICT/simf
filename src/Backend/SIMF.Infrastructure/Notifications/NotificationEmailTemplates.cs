using System.Net;
using SIMF.Domain.Notifications;

namespace SIMF.Infrastructure.Notifications;

/// <summary>
/// Inline bilingual email templates for the P13 — D-054 lifecycle
/// notifications. Token substitution is the dumbest possible
/// <c>string.Replace</c>; one template per kind, EN + AR.
///
/// <para>D-108: switched from string-keyed lookup to
/// <see cref="NotificationKind"/>-keyed lookup so a typo in a template
/// dispatch can't slip past the compiler.</para>
///
/// <para>The templates are intentionally minimal — one paragraph each.
/// A future re-skin (header logo, footer, brand colours) can swap the
/// implementation behind <see cref="Render"/> without changing every
/// call site.</para>
/// </summary>
internal static class NotificationEmailTemplates
{
    /// <summary>Renders the HTML body for one notification kind.</summary>
    /// <param name="kind">The dispatch kind.</param>
    /// <param name="culture">Two-letter culture (<c>en</c> or <c>ar</c>).</param>
    /// <param name="tokens">Substitution tokens (e.g. <c>DisplayName</c>,
    /// <c>QrId</c>, <c>Reason</c>). Every value is HTML-encoded before
    /// substitution.</param>
    public static string Render(
        NotificationKind kind, string culture, IReadOnlyDictionary<string, string> tokens)
    {
        var template = LookupTemplate(kind, culture);
        if (string.IsNullOrEmpty(template))
        {
            return tokens.TryGetValue("Body", out var body)
                ? $"<p>{WebUtility.HtmlEncode(body)}</p>"
                : $"<p>SIMF notification — {WebUtility.HtmlEncode(kind.ToString())}</p>";
        }
        foreach (var (name, raw) in tokens)
        {
            template = template.Replace(
                "{" + name + "}", WebUtility.HtmlEncode(raw));
        }
        return template;
    }

    private static string LookupTemplate(NotificationKind kind, string culture) =>
        (kind, culture) switch
        {
            (NotificationKind.AccountProfileSubmitted, "ar") => ProfileSubmittedAr,
            (NotificationKind.AccountProfileSubmitted, _) => ProfileSubmittedEn,
            (NotificationKind.AccountApproved, "ar") => ApprovedAr,
            (NotificationKind.AccountApproved, _) => ApprovedEn,
            (NotificationKind.AccountRejected, "ar") => RejectedAr,
            (NotificationKind.AccountRejected, _) => RejectedEn,
            (NotificationKind.AccountTwoFactorReset, "ar") => TwoFactorResetAr,
            (NotificationKind.AccountTwoFactorReset, _) => TwoFactorResetEn,
            (NotificationKind.AdminPendingVisitor, "ar") => AdminPendingVisitorAr,
            (NotificationKind.AdminPendingVisitor, _) => AdminPendingVisitorEn,
            _ => string.Empty,
        };

    // -- English ---------------------------------------------------------------
    private const string ProfileSubmittedEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>Thank you for completing your SIMF profile. An administrator "
        + "will review your account shortly. You will receive an email "
        + "once the decision is made.</p>";

    private const string ApprovedEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>Your SIMF account has been approved. Your event QR id is "
        + "<strong>{QrId}</strong>. Sign in to view it on your profile "
        + "page.</p>";

    private const string RejectedEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>Your SIMF account was not approved.</p>"
        + "<p><strong>Reason:</strong> {Reason}</p>"
        + "<p>If you believe this is a mistake, please contact your event "
        + "coordinator.</p>";

    private const string TwoFactorResetEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>An administrator has reset the two-factor authentication "
        + "on your SIMF account.</p>"
        + "<p><strong>Reason:</strong> {Reason}</p>"
        + "<p>Sign in with your password and set up two-factor "
        + "authentication again from your profile page.</p>";

    private const string AdminPendingVisitorEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>A new visitor is awaiting approval: <strong>{SubjectEmail}</strong>. "
        + "Open the Control Panel to review their account.</p>";

    // -- Arabic ----------------------------------------------------------------
    private const string ProfileSubmittedAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>شكراً لاستكمال ملفك الشخصي في SIMF. سيقوم المسؤول بمراجعة حسابك قريباً. "
        + "ستصلك رسالة بريد إلكتروني فور اتخاذ القرار.</p>";

    private const string ApprovedAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>تم اعتماد حسابك في SIMF. رمز QR الخاص بك للفعالية هو "
        + "<strong>{QrId}</strong>. سجّل الدخول لعرضه في ملفك الشخصي.</p>";

    private const string RejectedAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>لم يتم اعتماد حسابك في SIMF.</p>"
        + "<p><strong>السبب:</strong> {Reason}</p>"
        + "<p>إذا كنت تعتقد أن هذا خطأ، يُرجى التواصل مع منسّق الفعالية.</p>";

    private const string TwoFactorResetAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>قام أحد المسؤولين بإعادة تعيين المصادقة الثنائية على حسابك في SIMF.</p>"
        + "<p><strong>السبب:</strong> {Reason}</p>"
        + "<p>سجّل الدخول بكلمة المرور وقم بإعداد المصادقة الثنائية مرة أخرى من صفحة ملفك الشخصي.</p>";

    private const string AdminPendingVisitorAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>هناك زائر جديد بانتظار الموافقة: <strong>{SubjectEmail}</strong>. "
        + "افتح لوحة التحكم لمراجعة حسابه.</p>";
}
