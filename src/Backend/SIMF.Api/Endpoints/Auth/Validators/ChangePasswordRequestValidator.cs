using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Validates the change-password request (SIMF-API-001 section 12.5).</summary>
public sealed class ChangePasswordRequestValidator : Validator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword)
            .NotEmpty().Bilingual(
                "The current password is required.",
                "كلمة المرور الحالية مطلوبة.")
            // Cap the field at the same 128-character ceiling
            // StrongPassword enforces on NewPassword. Without this cap a
            // caller can post a megabyte string and the endpoint hashes
            // it with PBKDF2 inside a transaction — cheap authenticated
            // DoS vector.
            .MaximumLength(128).Bilingual(
                "The current password is too long.",
                "كلمة المرور الحالية طويلة جداً.");

        RuleFor(request => request.NewPassword).StrongPassword();

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword)
            .Bilingual(
                "The passwords do not match.",
                "كلمتا المرور غير متطابقتين.");
    }
}
