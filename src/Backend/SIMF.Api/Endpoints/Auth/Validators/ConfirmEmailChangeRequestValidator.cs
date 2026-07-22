using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>#24 — validates the confirm-email-change request.</summary>
public sealed class ConfirmEmailChangeRequestValidator : Validator<ConfirmEmailChangeRequest>
{
    public ConfirmEmailChangeRequestValidator()
    {
        RuleFor(request => request.NewEmail)
            .NotEmpty().Bilingual(
                "Email is required.",
                "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256).Bilingual(
                "The email address is too long.",
                "البريد الإلكتروني طويل جدًا.");

        RuleFor(request => request.Code)
            .NotEmpty().Bilingual(
                "The verification code is required.",
                "رمز التحقق مطلوب.")
            .Matches(@"^\d{6}$").Bilingual(
                "The verification code is six digits.",
                "رمز التحقق يتكوّن من ستة أرقام.");

        RuleFor(request => request.CurrentPassword)
            .NotEmpty().Bilingual(
                "Your current password is required.",
                "كلمة المرور الحالية مطلوبة.");
    }
}
