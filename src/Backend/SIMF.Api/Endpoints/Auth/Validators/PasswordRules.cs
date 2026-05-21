using FluentValidation;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>
/// The shared SIMF password policy (SIMF-API-001 section 12.5). Defined once so
/// every endpoint that accepts a new password enforces the same rules.
/// </summary>
internal static class PasswordRules
{
    public static IRuleBuilderOptions<T, string> StrongPassword<T>(
        this IRuleBuilder<T, string> rule) =>
        rule
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password must be at most 128 characters.")
            .Matches(@"\d").WithMessage("Password must contain a digit.")
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.");
}
