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
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256);

        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("The reset code is required.")
            .Matches(@"^\d{6}$").WithMessage("The reset code is six digits.");

        RuleFor(request => request.NewPassword)
            .StrongPassword()
            .Must((request, password) =>
                !string.Equals(password, request.Email, StringComparison.OrdinalIgnoreCase))
            .WithMessage("Password must not be the same as the email address.");

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword)
            .WithMessage("The passwords do not match.");
    }
}
