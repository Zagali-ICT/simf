using FastEndpoints;
using FluentValidation;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Auth.Validators;

/// <summary>Part B — validates a badge-resolve request: the QR id is required.</summary>
public sealed class ResolveBadgeRequestValidator : Validator<ResolveBadgeRequest>
{
    public ResolveBadgeRequestValidator()
    {
        RuleFor(request => request.QrId)
            .NotEmpty().Bilingual(
                "The badge code is required.",
                "رمز الشارة مطلوب.")
            .MaximumLength(64);
    }
}

/// <summary>Part B — validates a badge sign-in request: the QR id is required;
/// the password is required (shape only — the account's password policy was
/// enforced when the password was first set, so it is never re-checked here).</summary>
public sealed class BadgeSignInRequestValidator : Validator<BadgeSignInRequest>
{
    public BadgeSignInRequestValidator()
    {
        RuleFor(request => request.QrId)
            .NotEmpty().Bilingual(
                "The badge code is required.",
                "رمز الشارة مطلوب.")
            .MaximumLength(64);

        RuleFor(request => request.Password)
            .NotEmpty().Bilingual("Password is required.", "كلمة المرور مطلوبة.");
    }
}

/// <summary>Part B — validates the activation-start request: QR id required;
/// the email, when supplied, must be a valid address.</summary>
public sealed class BadgeActivationStartRequestValidator : Validator<BadgeActivationStartRequest>
{
    public BadgeActivationStartRequestValidator()
    {
        RuleFor(request => request.QrId)
            .NotEmpty().Bilingual(
                "The badge code is required.",
                "رمز الشارة مطلوب.")
            .MaximumLength(64);

        When(request => !string.IsNullOrWhiteSpace(request.Email), () =>
        {
            RuleFor(request => request.Email!)
                .EmailAddress().Bilingual(
                    "A valid email address is required.",
                    "يجب إدخال بريد إلكتروني صالح.")
                .MaximumLength(256);
        });
    }
}

/// <summary>Part B — validates the activation-complete request: QR id + a
/// six-digit code + a strong, confirmed first password.</summary>
public sealed class BadgeActivationCompleteRequestValidator : Validator<BadgeActivationCompleteRequest>
{
    public BadgeActivationCompleteRequestValidator()
    {
        RuleFor(request => request.QrId)
            .NotEmpty().Bilingual(
                "The badge code is required.",
                "رمز الشارة مطلوب.")
            .MaximumLength(64);

        RuleFor(request => request.Code)
            .NotEmpty().Bilingual(
                "The verification code is required.",
                "رمز التحقق مطلوب.")
            .Matches(@"^\d{6}$").Bilingual(
                "The verification code is six digits.",
                "رمز التحقق يتكوّن من ستة أرقام.");

        RuleFor(request => request.NewPassword)
            .StrongPassword();

        RuleFor(request => request.ConfirmPassword)
            .Equal(request => request.NewPassword)
            .Bilingual(
                "The passwords do not match.",
                "كلمتا المرور غير متطابقتين.");
    }
}
