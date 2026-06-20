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
            .StrongPassword()
            .Must((request, password) =>
                !SIMF.Common.PasswordPolicy.ResemblesIdentifier(password, request.Email))
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
