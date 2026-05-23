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
            .NotEmpty().Bilingual(
                "The sign-in token is required.",
                "رمز الجلسة مطلوب.");

        RuleFor(request => request.Code)
            .NotEmpty().Bilingual(
                "The verification code is required.",
                "رمز التحقق مطلوب.")
            .Matches(@"^\d{6}$").Bilingual(
                "The verification code is six digits.",
                "رمز التحقق يتكوّن من ستة أرقام.");
    }
}
