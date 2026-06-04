using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// Validates the admin duplicate-user request (decision D-045, H1 hardening
/// of D-044 b). Uses the same email-shape rule as
/// <see cref="AdminCreateUserRequestValidator"/> — a duplicate without a
/// real email becomes a zombie account that cannot receive its invitation.
/// </summary>
public sealed class AdminDuplicateUserRequestValidator : Validator<AdminDuplicateUserRequest>
{
    public AdminDuplicateUserRequestValidator()
    {
        RuleFor(request => request.SourceId)
            .NotEqual(Guid.Empty).Bilingual(
                "The source user id is required.",
                "معرّف المستخدم المصدر مطلوب.");

        RuleFor(request => request.NewEmail)
            .NotEmpty().Bilingual(
                "A new email address is required.",
                "البريد الإلكتروني الجديد مطلوب.")
            .EmailAddress().Bilingual(
                "A valid email address is required.",
                "يجب إدخال بريد إلكتروني صالح.")
            .MaximumLength(256);
    }
}
