using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the verify-otp request.</summary>
public sealed class VerifyOtpRequestValidator : Validator<VerifyOtpRequest>
{
    public VerifyOtpRequestValidator()
    {
        RuleFor(request => request.OtpToken)
            .NotEmpty().WithMessage("The sign-in token is required.");

        RuleFor(request => request.Code)
            .NotEmpty().WithMessage("The verification code is required.")
            .Matches(@"^\d{6}$").WithMessage("The verification code is six digits.");
    }
}
