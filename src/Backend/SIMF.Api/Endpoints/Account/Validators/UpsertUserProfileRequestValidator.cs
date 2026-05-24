using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Common;
using SIMF.Contracts.UserProfile;

namespace SIMF.Api.Endpoints.Account.Validators;

/// <summary>
/// Validates the user-profile upsert (decisions D-046 b, P8 — D-049;
/// renamed from <c>UpsertVisitorProfileRequestValidator</c>). The
/// <c>VisitorType</c> string-discriminator rule was dropped in P8 — the
/// profile-type now flows through the <c>UserProfile.ProfileTypeId</c>
/// FK assigned by an admin, not a free-text claim by the user. The
/// phone rules are deliberately permissive — any country code + local
/// number is accepted ("any phone, not only Saudi, with +xx" — user
/// guidance during D-046 design). The nationality must match the
/// curated list (<see cref="Countries"/>); a stranger code is rejected.
/// </summary>
public sealed class UpsertUserProfileRequestValidator
    : Validator<UpsertUserProfileRequest>
{
    // Permissive phone shape: optional "+" + 1-4 digit country code, then
    // an optional separator (space or "-") and 4-15 more digits. Trims
    // typical whitespace before checking.
    private static readonly System.Text.RegularExpressions.Regex PhoneShape =
        new(@"^\+?\d{1,4}[-\s]?\d{4,15}$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    public UpsertUserProfileRequestValidator()
    {
        RuleFor(request => request.ArabicName)
            .NotEmpty().Bilingual(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.")
            .MaximumLength(256);

        RuleFor(request => request.EnglishName)
            .NotEmpty().Bilingual(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.")
            .MaximumLength(256);

        RuleFor(request => request.NationalityCode)
            .NotEmpty().Bilingual(
                "Nationality is required.",
                "الجنسية مطلوبة.")
            .Must(code => Countries.IsKnown(code))
            .WithErrorCode(ErrorCodes.ProfileNationalityUnknown)
            .Bilingual(
                "Nationality is not in the supported list.",
                "الجنسية غير موجودة في القائمة المدعومة.");

        RuleFor(request => request.PlaceOfBirth)
            .MaximumLength(128);

        // Conditional ID fields (P5 hardening — strict national-id prefix
        // rules per the Saudi numbering plan):
        //   - Saudi national id is exactly 10 digits and starts with 1.
        //   - Iqama (residency permit) is exactly 10 digits and starts with 2.
        //   - Non-Saudi users who don't hold an Iqama carry a passport.
        When(request => request.IsSaudi, () =>
        {
            RuleFor(r => r.NationalId)
                .NotEmpty().Bilingual(
                    "The Saudi national id is required.",
                    "رقم الهوية الوطنية مطلوب.")
                .Matches(@"^1\d{9}$").Bilingual(
                    "The Saudi national id must be 10 digits starting with 1.",
                    "يجب أن يتكوّن رقم الهوية الوطنية من 10 أرقام تبدأ بالرقم 1.");
        });

        When(request => !request.IsSaudi, () =>
        {
            RuleFor(r => r.IqamaNumber)
                .Matches(@"^2\d{9}$").When(r => !string.IsNullOrEmpty(r.IqamaNumber))
                .Bilingual(
                    "An Iqama number must be 10 digits starting with 2.",
                    "يجب أن يتكوّن رقم الإقامة من 10 أرقام تبدأ بالرقم 2.");

            RuleFor(r => r.PassportNumber)
                .MaximumLength(32).When(r => !string.IsNullOrEmpty(r.PassportNumber))
                .Bilingual(
                    "Passport number must be 32 characters or less.",
                    "يجب ألا يتجاوز رقم جواز السفر 32 حرفًا.");

            RuleFor(r => r)
                .Must(r => !string.IsNullOrEmpty(r.IqamaNumber)
                    || !string.IsNullOrEmpty(r.PassportNumber))
                .Bilingual(
                    "Either Iqama or Passport number is required.",
                    "يجب إدخال رقم الإقامة أو رقم جواز السفر.");
        });

        RuleFor(request => request.SaudiMobile)
            .Must(value => string.IsNullOrEmpty(value) || PhoneShape.IsMatch(value.Trim()))
            .Bilingual(
                "The Saudi mobile is not a recognised phone number.",
                "رقم الجوال السعودي ليس بصيغة معروفة.");

        RuleFor(request => request.InternationalMobile)
            .Must(value => string.IsNullOrEmpty(value) || PhoneShape.IsMatch(value.Trim()))
            .Bilingual(
                "The international mobile is not a recognised phone number.",
                "رقم الجوال الدولي ليس بصيغة معروفة.");
    }
}
