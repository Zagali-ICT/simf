using Microsoft.AspNetCore.Identity;
using SIMF.Common;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// A7-21 / A7-28 / A7-29 (NCA Secure Application-Development Standard) — the central
/// ASP.NET-Identity password validator. Every credential path that runs through
/// <see cref="UserManager{TUser}"/> (sign-up, admin-create, reset, change, the
/// seeder) is checked here against the shared <see cref="PasswordPolicy"/>, in
/// addition to the request-time FluentValidation pass — so the policy holds even
/// for non-HTTP callers. ASP.NET's built-in length check still runs via
/// <c>options.Password.RequiredLength</c>; this validator owns the content rules.
/// </summary>
internal sealed class SimfPasswordValidator : IPasswordValidator<SimfUser>
{
    public Task<IdentityResult> ValidateAsync(
        UserManager<SimfUser> manager, SimfUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Task.FromResult(IdentityResult.Failed(new IdentityError
            {
                Code = "PasswordRequired",
                Description = "Password is required.",
            }));
        }

        var errors = new List<IdentityError>();
        void Add(string code, string description) =>
            errors.Add(new IdentityError { Code = code, Description = description });

        if (password.Length > PasswordPolicy.MaxLength)
        {
            Add("PasswordTooLong", $"Password must be at most {PasswordPolicy.MaxLength} characters.");
        }
        if (!PasswordPolicy.HasUppercase(password))
        {
            Add("PasswordRequiresUpper", "Password must contain an upper-case letter.");
        }
        if (!PasswordPolicy.HasLowercase(password))
        {
            Add("PasswordRequiresLower", "Password must contain a lower-case letter.");
        }
        if (!PasswordPolicy.HasDigit(password))
        {
            Add("PasswordRequiresDigit", "Password must contain a digit.");
        }
        if (!PasswordPolicy.HasSpecial(password))
        {
            Add("PasswordRequiresSpecial", "Password must contain a special character.");
        }
        if (PasswordPolicy.HasRepeatRun(password))
        {
            Add("PasswordRepeatRun", "Password must not repeat the same character three or more times.");
        }
        if (PasswordPolicy.HasSequentialRun(password))
        {
            Add("PasswordSequentialRun", "Password must not contain sequential characters such as \"123\" or \"abc\".");
        }
        if (PasswordPolicy.IsCommon(password))
        {
            Add("PasswordTooCommon", "Password is too common; choose a less predictable password.");
        }
        if (PasswordPolicy.ResemblesIdentifier(password, user.Email))
        {
            Add("PasswordEqualsIdentifier", "Password must not match the email address.");
        }

        return Task.FromResult(errors.Count == 0
            ? IdentityResult.Success
            : IdentityResult.Failed([.. errors]));
    }
}
