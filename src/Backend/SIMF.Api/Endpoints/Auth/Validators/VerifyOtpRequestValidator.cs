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
