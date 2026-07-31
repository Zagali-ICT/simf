using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>#2 — validates the mandatory-enrolment start request.</summary>
public sealed class StartTwoFactorEnrolmentRequestValidator
    : Validator<StartTwoFactorEnrolmentRequest>
{
    public StartTwoFactorEnrolmentRequestValidator()
    {
        RuleFor(request => request.EnrolmentToken)
            .NotEmpty().Bilingual(
                "The enrolment ticket is required.",
                "تذكرة التسجيل مطلوبة.");
    }
}

/// <summary>#2 — validates the mandatory-enrolment completion request.</summary>
public sealed class CompleteTwoFactorEnrolmentRequestValidator
    : Validator<CompleteTwoFactorEnrolmentRequest>
{
    public CompleteTwoFactorEnrolmentRequestValidator()
    {
        RuleFor(request => request.EnrolmentToken)
            .NotEmpty().Bilingual(
                "The enrolment ticket is required.",
                "تذكرة التسجيل مطلوبة.");

        RuleFor(request => request.Code)
            .NotEmpty().Bilingual(
                "The verification code is required.",
                "رمز التحقق مطلوب.")
            .Matches(@"^\d{6}$").Bilingual(
                "The verification code is six digits.",
                "رمز التحقق يتكوّن من ستة أرقام.");
    }
}
