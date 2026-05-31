using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>D-206: validates the complete-password-change request.</summary>
public sealed class CompletePasswordChangeRequestValidator : Validator<CompletePasswordChangeRequest>
{
    public CompletePasswordChangeRequestValidator()
    {
        RuleFor(request => request.PasswordChangeToken)
            .NotEmpty().Bilingual(
                "The sign-in session is required.",
                "جلسة تسجيل الدخول مطلوبة.");

        RuleFor(request => request.NewPassword).StrongPassword();

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword)
            .Bilingual(
                "The passwords do not match.",
                "كلمتا المرور غير متطابقتين.");
    }
}
