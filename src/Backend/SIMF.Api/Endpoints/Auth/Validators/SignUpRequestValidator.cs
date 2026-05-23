using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>
/// Validates the sign-up request, including the password policy of
/// SIMF-API-001 section 12.5 — enforced here, in one place.
/// </summary>
public sealed class SignUpRequestValidator : Validator<SignUpRequest>
{
    public SignUpRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().Bilingual("Email is required.", "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty().Bilingual("Password is required.", "كلمة المرور مطلوبة.")
            .MinimumLength(8).Bilingual(
                "The password must be at least 8 characters.",
                "يجب أن تتكوّن كلمة المرور من 8 أحرف على الأقل.")
            .MaximumLength(128).Bilingual(
                "The password must be at most 128 characters.",
                "يجب ألا تتجاوز كلمة المرور 128 حرفًا.")
            .Matches("[A-Za-z]").Bilingual(
                "The password must contain at least one letter.",
                "يجب أن تحتوي كلمة المرور على حرف واحد على الأقل.")
            .Matches("[0-9]").Bilingual(
                "The password must contain at least one digit.",
                "يجب أن تحتوي كلمة المرور على رقم واحد على الأقل.")
            .Must((request, password) =>
                !string.Equals(password, request.Email, StringComparison.OrdinalIgnoreCase))
            .Bilingual(
                "The password must not be the same as the email address.",
                "يجب ألا تكون كلمة المرور مطابقة لعنوان البريد الإلكتروني.");

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.Password)
            .Bilingual(
                "The passwords do not match.",
                "كلمتا المرور غير متطابقتين.");
    }
}
