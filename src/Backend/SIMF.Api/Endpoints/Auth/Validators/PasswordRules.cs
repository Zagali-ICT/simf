using FluentValidation;
using SIMF.Common;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>
/// The shared SIMF password policy (SIMF-API-001 section 12.5, hardened to the
/// NCA Secure Application-Development Standard A7-10/A7-28/A7-29). Defined once so
/// every endpoint that accepts a new password enforces the same rules; the
/// structural checks delegate to <see cref="PasswordPolicy"/> so the request
/// validators and the central Identity password validator stay identical.
/// </summary>
internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().Bilingual("Password is required.", "كلمة المرور مطلوبة.")
            .MinimumLength(PasswordPolicy.MinLength).Bilingual(
                "Password must be at least 8 characters.",
                "يجب أن تتكوّن كلمة المرور من 8 أحرف على الأقل.")
            .MaximumLength(PasswordPolicy.MaxLength).Bilingual(
                "Password must be at most 128 characters.",
                "يجب ألا تتجاوز كلمة المرور 128 حرفًا.")
            .Must(PasswordPolicy.HasUppercase).Bilingual(
                "Password must contain an upper-case letter.",
                "يجب أن تحتوي كلمة المرور على حرف كبير واحد على الأقل.")
            .Must(PasswordPolicy.HasLowercase).Bilingual(
                "Password must contain a lower-case letter.",
                "يجب أن تحتوي كلمة المرور على حرف صغير واحد على الأقل.")
            .Must(PasswordPolicy.HasDigit).Bilingual(
                "Password must contain a digit.",
                "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل.")
            .Must(PasswordPolicy.HasSpecial).Bilingual(
                "Password must contain a special character.",
                "يجب أن تحتوي كلمة المرور على رمز خاص واحد على الأقل.")
            .Must(password => !PasswordPolicy.HasRepeatRun(password)).Bilingual(
                "Password must not repeat the same character three or more times in a row.",
                "يجب ألا تحتوي كلمة المرور على ثلاثة أحرف متطابقة متتالية أو أكثر.")
            .Must(password => !PasswordPolicy.HasSequentialRun(password)).Bilingual(
                "Password must not contain sequential characters such as \"123\" or \"abc\".",
                "يجب ألا تحتوي كلمة المرور على أحرف متسلسلة مثل \"123\" أو \"abc\".")
            .Must(password => !PasswordPolicy.IsCommon(password)).Bilingual(
                "Password is too common; choose a less predictable password.",
                "كلمة المرور شائعة جدًا؛ اختر كلمة مرور أقل قابلية للتخمين.");
}
