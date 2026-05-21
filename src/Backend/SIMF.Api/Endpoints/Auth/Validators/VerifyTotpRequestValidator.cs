using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the verify-totp request.</summary>
public sealed class VerifyTotpRequestValidator : Validator<VerifyTotpRequest>
{
    public VerifyTotpRequestValidator()
    {
        RuleFor(request => request.MfaToken)
            .NotEmpty().WithMessage("The sign-in token is required.");

        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("The verification code is required.")
            .Matches(@"^\d{6}$").WithMessage("The verification code is six digits.");
    }
}
