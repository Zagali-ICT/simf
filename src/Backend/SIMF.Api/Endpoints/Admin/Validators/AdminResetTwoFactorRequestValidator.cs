using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>Validates the admin-reset-2FA request.</summary>
public sealed class AdminResetTwoFactorRequestValidator
    : Validator<AdminResetTwoFactorRequest>
{
    public AdminResetTwoFactorRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().Bilingual("Email is required.", "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);

        RuleFor(request => request.Reason)
            .NotEmpty().Bilingual(
                "A reason is required.",
                "السبب مطلوب.")
            .MinimumLength(10).Bilingual(
                "The reason must be at least 10 characters.",
                "يجب أن يتكوّن السبب من 10 أحرف على الأقل.")
            .MaximumLength(500).Bilingual(
                "The reason must be at most 500 characters.",
                "يجب ألا يتجاوز السبب 500 حرفًا.");
    }
}
