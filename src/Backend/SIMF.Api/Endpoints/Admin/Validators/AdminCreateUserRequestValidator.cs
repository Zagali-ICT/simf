using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>Validates the admin-create-user request (D-042).</summary>
public sealed class AdminCreateUserRequestValidator : Validator<AdminCreateUserRequest>
{
    public AdminCreateUserRequestValidator()
    {
        RuleFor(request => request.Email)
            .NotEmpty().Bilingual("Email is required.", "البريد الإلكتروني مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);

        RuleFor(request => request.DisplayName)
            .NotEmpty().Bilingual(
                "Display name is required.",
                "الاسم المعروض مطلوب.")
            .MinimumLength(2).Bilingual(
                "Display name must be at least 2 characters.",
                "يجب أن يتكوّن الاسم المعروض من حرفين على الأقل.")
            .MaximumLength(128).Bilingual(
                "Display name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم المعروض 128 حرفًا.");
    }
}
