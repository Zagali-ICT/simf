using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the reset-password request (SIMF-API-001 section 12.5).</summary>
public sealed class ResetPasswordRequestValidator : Validator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().Bilingual("Email is required.", "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);

        RuleFor(request => request.Code)
            .NotEmpty().Bilingual(
                "The reset code is required.",
                "رمز إعادة التعيين مطلوب.")
            .Matches(@"^\d{6}$").Bilingual(
                "The reset code is six digits.",
                "رمز إعادة التعيين يتكوّن من ستة أرقام.");

        RuleFor(request => request.NewPassword)
            .StrongPassword()
            .Must((request, password) =>
                !string.Equals(password, request.Email, StringComparison.OrdinalIgnoreCase))
            .Bilingual(
                "Password must not be the same as the email address.",
                "يجب ألا تكون كلمة المرور مطابقة لعنوان البريد الإلكتروني.");

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword)
            .Bilingual(
                "The passwords do not match.",
                "كلمتا المرور غير متطابقتين.");
    }
}
