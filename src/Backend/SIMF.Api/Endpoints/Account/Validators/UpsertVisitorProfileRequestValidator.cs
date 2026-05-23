using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Common;
using SIMF.Contracts.VisitorProfile;

namespace SIMF.Api.Endpoints.Account.Validators;

/// <summary>
/// Validates the visitor-profile upsert (decision D-046 b, myComment #18).
/// The phone rules are deliberately permissive — any country code + local
/// number is accepted ("any phone, not only Saudi, with +xx" — user
/// guidance during D-046 design). The nationality must match the curated
/// list (<see cref="Countries"/>); a stranger code is rejected.
/// </summary>
public sealed class UpsertVisitorProfileRequestValidator
    : Validator<UpsertVisitorProfileRequest>
{
    // Permissive phone shape: optional "+" + 1-4 digit country code, then
    // an optional separator (space or "-") and 4-15 more digits. Trims
    // typical whitespace before checking.
    private static readonly System.Text.RegularExpressions.Regex PhoneShape =
        new(@"^\+?\d{1,4}[-\s]?\d{4,15}$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly string[] AllowedVisitorTypes =
        { "Visitor", "Exhibitor", "Press" };

    public UpsertVisitorProfileRequestValidator()
    {
        RuleFor(request => request.VisitorType)
            .NotEmpty().Bilingual(
                "The visitor type is required.",
                "نوع الزائر مطلوب.")
            .Must(value => AllowedVisitorTypes.Contains(value, StringComparer.OrdinalIgnoreCase))
            .Bilingual(
                "The visitor type must be one of: Visitor, Exhibitor, Press.",
                "نوع الزائر يجب أن يكون: زائر، عارض، صحفي.");

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
            .WithErrorCode(ErrorCodes.VisitorNationalityUnknown)
            .Bilingual(
                "Nationality is not in the supported list.",
                "الجنسية غير موجودة في القائمة المدعومة.");

        RuleFor(request => request.PlaceOfBirth)
            .MaximumLength(128);

        // Conditional ID fields — Saudis carry the 10-digit national id;
        // non-Saudi residents carry an Iqama; non-Saudi visitors carry
        // a passport.
        When(request => request.IsSaudi, () =>
        {
            RuleFor(r => r.NationalId)
                .NotEmpty().Bilingual(
                    "The Saudi national id is required.",
                    "رقم الهوية الوطنية مطلوب.")
                .Matches(@"^\d{10}$").Bilingual(
                    "The Saudi national id must be exactly 10 digits.",
                    "يجب أن يتكوّن رقم الهوية الوطنية من 10 أرقام بالضبط.");
        });

        When(request => !request.IsSaudi, () =>
        {
            RuleFor(r => r.IqamaNumber)
                .Matches(@"^\d{10}$").When(r => !string.IsNullOrEmpty(r.IqamaNumber))
                .Bilingual(
                    "An Iqama number must be exactly 10 digits.",
                    "يجب أن يتكوّن رقم الإقامة من 10 أرقام بالضبط.");

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
