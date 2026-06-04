using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the totp-disable request.</summary>
public sealed class TotpDisableRequestValidator : Validator<TotpDisableRequest>
{
    public TotpDisableRequestValidator()
    {
        RuleFor(request => request.Code)
            .NotEmpty().Bilingual(
                "The verification code is required.",
                "رمز التحقق مطلوب.")
            .Matches(@"^\d{6}$").Bilingual(
                "The verification code is six digits.",
                "رمز التحقق يتكوّن من ستة أرقام.");
    }
}
