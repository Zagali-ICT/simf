using FluentValidation;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>
/// The shared SIMF password policy (SIMF-API-001 section 12.5). Defined once so
/// every endpoint that accepts a new password enforces the same rules.
/// </summary>
internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().Bilingual("Password is required.", "كلمة المرور مطلوبة.")
            .MinimumLength(8).Bilingual(
                "Password must be at least 8 characters.",
                "يجب أن تتكوّن كلمة المرور من 8 أحرف على الأقل.")
            .MaximumLength(128).Bilingual(
                "Password must be at most 128 characters.",
                "يجب ألا تتجاوز كلمة المرور 128 حرفًا.")
            .Matches(@"\d").Bilingual(
                "Password must contain a digit.",
                "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل.")
            .Matches("[A-Za-z]").Bilingual(
                "Password must contain a letter.",
                "يجب أن تحتوي كلمة المرور على حرف واحد على الأقل.");
}
