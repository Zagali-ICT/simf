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
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("The password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("The password must be at most 128 characters.")
            .Matches("[A-Za-z]").WithMessage("The password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("The password must contain at least one digit.")
            .Must((request, password) =>
                !string.Equals(password, request.Email, StringComparison.OrdinalIgnoreCase))
            .WithMessage("The password must not be the same as the email address.");

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.Password)
            .WithMessage("The passwords do not match.");
    }
}
