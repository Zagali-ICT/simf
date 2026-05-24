using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the sign-in request shape (not the password policy).</summary>
public sealed class SignInRequestValidator : Validator<SignInRequest>
{
    public SignInRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().Bilingual("Email is required.", "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty().Bilingual("Password is required.", "كلمة المرور مطلوبة.");

        // P2 — audience must be one of the declared enum values; a stray
        // integer is rejected at the validator boundary, not at the gate.
        RuleFor(request => request.Audience)
            .IsInEnum().Bilingual(
                "The sign-in surface is not valid.",
                "واجهة تسجيل الدخول غير صالحة.");
    }
}
