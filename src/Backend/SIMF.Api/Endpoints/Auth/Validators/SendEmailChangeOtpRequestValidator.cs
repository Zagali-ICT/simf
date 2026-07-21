using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>#24 — validates the send-email-change-code request.</summary>
public sealed class SendEmailChangeOtpRequestValidator : Validator<SendEmailChangeOtpRequest>
{
    public SendEmailChangeOtpRequestValidator()
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
    }
}
