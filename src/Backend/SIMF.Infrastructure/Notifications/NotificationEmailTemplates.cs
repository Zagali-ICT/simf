using System.Net;

namespace SIMF.Infrastructure.Notifications;

/// <summary>
/// Inline bilingual email templates for the P13 — D-054 lifecycle
/// notifications. Token substitution is the dumbest possible
/// <c>string.Replace</c>; one template per kind, EN + AR.
///
/// <para>The templates are intentionally minimal — one paragraph each.
/// A future re-skin (header logo, footer, brand colours) can swap the
/// implementation behind <see cref="Render"/> without changing every
/// call site.</para>
/// </summary>
internal static class NotificationEmailTemplates
{
    /// <summary>Renders the HTML body for one notification kind.</summary>
    /// <param name="kind">Stable kind name (e.g. <c>Account.Approved</c>).</param>
    /// <param name="culture">Two-letter culture (<c>en</c> or <c>ar</c>).</param>
    /// <param name="tokens">Substitution tokens (e.g. <c>DisplayName</c>,
    /// <c>QrId</c>, <c>Reason</c>). Every value is HTML-encoded before
    /// substitution.</param>
    public static string Render(
        string kind, string culture, IReadOnlyDictionary<string, string> tokens)
    {
        var template = LookupTemplate(kind, culture);
        if (string.IsNullOrEmpty(template))
        {
            // No template registered → render a one-line fallback so the
            // email still says something useful.
            return tokens.TryGetValue("Body", out var body)
                ? $"<p>{WebUtility.HtmlEncode(body)}</p>"
                : $"<p>SIMF notification — {WebUtility.HtmlEncode(kind)}</p>";
        }
        foreach (var (name, raw) in tokens)
        {
            template = template.Replace(
                "{" + name + "}", WebUtility.HtmlEncode(raw));
        }
        return template;
    }

    private static string LookupTemplate(string kind, string culture) =>
        (kind, culture) switch
        {
            ("Account.ProfileSubmitted", "ar") => ProfileSubmittedAr,
            ("Account.ProfileSubmitted", _) => ProfileSubmittedEn,
            ("Account.Approved", "ar") => ApprovedAr,
            ("Account.Approved", _) => ApprovedEn,
            ("Account.Rejected", "ar") => RejectedAr,
            ("Account.Rejected", _) => RejectedEn,
            ("Account.TwoFactorReset", "ar") => TwoFactorResetAr,
            ("Account.TwoFactorReset", _) => TwoFactorResetEn,
            ("Account.PasswordInvited", "ar") => PasswordInvitedAr,
            ("Account.PasswordInvited", _) => PasswordInvitedEn,
            ("Account.PasswordChanged", "ar") => PasswordChangedAr,
            ("Account.PasswordChanged", _) => PasswordChangedEn,
            ("Account.PasswordReset", "ar") => PasswordResetAr,
            ("Account.PasswordReset", _) => PasswordResetEn,
            ("Admin.PendingVisitor", "ar") => AdminPendingVisitorAr,
            ("Admin.PendingVisitor", _) => AdminPendingVisitorEn,
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

    private const string PasswordInvitedEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>A SIMF account has been created for you. To activate it, "
        + "open the Control Panel and set your password with the "
        + "verification code below.</p>"
        + "<p><strong>Email:</strong> {Email}<br/>"
        + "<strong>Code:</strong> <strong>{Code}</strong><br/>"
        + "<strong>Valid for:</strong> {Days} days.</p>";

    private const string PasswordChangedEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>Your SIMF password was changed. If you did not do this, "
        + "contact your security team immediately.</p>";

    private const string PasswordResetEn =
        "<p>Hello {DisplayName},</p>"
        + "<p>Your SIMF password was reset successfully. You can sign in "
        + "with your new password now.</p>";

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

    private const string PasswordInvitedAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>تم إنشاء حساب لك في SIMF. لتفعيله، افتح لوحة التحكم وعيّن كلمة المرور باستخدام رمز التحقق أدناه.</p>"
        + "<p><strong>البريد الإلكتروني:</strong> {Email}<br/>"
        + "<strong>الرمز:</strong> <strong>{Code}</strong><br/>"
        + "<strong>صالح لمدة:</strong> {Days} أيام.</p>";

    private const string PasswordChangedAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>تم تغيير كلمة المرور الخاصة بك في SIMF. إذا لم تكن أنت من قام بذلك، "
        + "يُرجى التواصل مع فريق الأمن فوراً.</p>";

    private const string PasswordResetAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>تمت إعادة تعيين كلمة المرور الخاصة بك في SIMF بنجاح. يمكنك تسجيل الدخول الآن بكلمة المرور الجديدة.</p>";

    private const string AdminPendingVisitorAr =
        "<p>مرحباً {DisplayName}،</p>"
        + "<p>هناك زائر جديد بانتظار الموافقة: <strong>{SubjectEmail}</strong>. "
        + "افتح لوحة التحكم لمراجعة حسابه.</p>";
}
