using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the verify-recovery-code request (D-040).</summary>
public sealed class VerifyRecoveryCodeRequestValidator : Validator<VerifyRecoveryCodeRequest>
{
    public VerifyRecoveryCodeRequestValidator()
    {
        RuleFor(request => request.MfaToken)
            .NotEmpty().Bilingual(
                "The sign-in token is required.",
                "رمز الجلسة مطلوب.");

        RuleFor(request => request.Code)
            .NotEmpty().Bilingual(
                "The recovery code is required.",
                "رمز الاسترداد مطلوب.")
            .MaximumLength(32).Bilingual(
                "The recovery code is too long.",
                "رمز الاسترداد طويل جدًا.");
    }
}
