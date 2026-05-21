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
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256);

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
