using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the change-password request (SIMF-API-001 section 12.5).</summary>
public sealed class ChangePasswordRequestValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty().Bilingual(
                "The current password is required.",
                "كلمة المرور الحالية مطلوبة.");

        RuleFor(request => request.NewPassword).StrongPassword();

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword)
            .Bilingual(
                "The passwords do not match.",
                "كلمتا المرور غير متطابقتين.");
    }
}
