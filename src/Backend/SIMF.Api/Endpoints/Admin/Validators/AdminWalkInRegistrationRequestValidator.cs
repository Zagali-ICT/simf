using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Account.Validators;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// Validates the walk-in registration request. Email is optional
/// (the desk frequently registers walk-ins without one); the matching
/// service synthesizes a placeholder.
///
/// <para>This validator owns the SHAPE rules only (lengths, the
/// national-id / Iqama patterns and their Luhn checksum, the Saudi and E.164
/// mobile formats, the plate format, the enum range), each applied when the
/// field is supplied. The PRESENCE rules — which of those fields are mandatory
/// — live in <c>AdminAccountService.EnsureFullDeskFields</c>, because whether
/// they are mandatory depends on whether the quick-register mode is
/// armed, and that cannot be read here: FluentValidation is synchronous and
/// FastEndpoints validators are singletons, so a constructor read would freeze
/// the answer at startup. Putting the choice in the service also keeps it
/// server-resolved, rather than a request flag any caller could set to opt
/// itself out of validation.</para>
///
/// <para>With the mode disarmed the service reproduces the original presence
/// checks with their exact bilingual messages, so the desk behaves as it always
/// has: staff are collecting these face-to-face, and incomplete submissions are
/// still pushed back rather than silently storing nulls.</para>
/// </summary>
public sealed class AdminWalkInRegistrationRequestValidator
    : Validator<AdminWalkInRegistrationRequest>
{
    public AdminWalkInRegistrationRequestValidator()
    {
        // Email — optional, but when supplied it must be valid (≤256 chars).
        When(request => !string.IsNullOrWhiteSpace(request.Email), () =>
        {
            RuleFor(request => request.Email!)
                .EmailAddress().Bilingual(
                    "A valid email address is required.",
                    "يجب إدخال بريد إلكتروني صالح.")
                .MaximumLength(256).Bilingual(
                    "Email must be at most 256 characters.",
                    "يجب ألا يتجاوز البريد الإلكتروني 256 حرفًا.");
        });

        RuleFor(request => request.DisplayName)
            .MaximumLength(128).Bilingual(
                "Display name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم المعروض 128 حرفًا.");

        RuleFor(request => request.ArabicName)
            .MaximumLength(50).Bilingual(
                "Arabic name must be at most 50 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 50 حرفًا.");

        RuleFor(request => request.EnglishName)
            .MaximumLength(50).Bilingual(
                "English name must be at most 50 characters.",
                "يجب ألا يتجاوز الاسم بالإنجليزية 50 حرفًا.");

        RuleFor(request => request.ProfileTypeId)
            .NotEqual(Guid.Empty).Bilingual(
                "Profile type is required.",
                "نوع الملف الشخصي مطلوب.");

        // Organisation (الجهة): required at the desk. Shape-check only here
        // (non-null, non-empty Guid); the existence / IsActive check runs in
        // the service against the App DB (cross-context — FluentValidation is
        // sync), exactly like ProfileTypeId / NationalityCode.
        RuleFor(request => request.NationalityCode)
            .Length(2, 3)
            .When(request => !string.IsNullOrWhiteSpace(request.NationalityCode))
            .Bilingual(
                "Nationality code must be 2-3 characters (ISO 3166-1).",
                "يجب أن يتكوّن رمز الجنسية من 2-3 أحرف (ISO 3166-1).");

        When(request => request.IsSaudi, () =>
        {
            RuleFor(request => request.NationalId)
                .Matches("^1[0-9]{9}$")
                .When(request => !string.IsNullOrWhiteSpace(request.NationalId))
                .Bilingual(
                    "The Saudi national ID is 10 digits and starts with 1.",
                    "الهوية الوطنية السعودية مكوّنة من 10 أرقام وتبدأ بالرقم 1.")
                // Apply the same Luhn checksum the self-service path
                // uses, so a malformed ID is rejected at the desk too.
                .Must(id => string.IsNullOrEmpty(id)
                    || UpsertUserProfileRequestValidator.IsValidLuhn(id))
                .Bilingual(
                    "The Saudi national ID is not a valid number.",
                    "رقم الهوية الوطنية غير صحيح.");
        }).Otherwise(() =>
        {
            // Non-Saudi: either Iqama OR Passport must be supplied. When the
            // operator picks Iqama, it must match the residency pattern
            // (10 digits starting with 2).
            When(request => !string.IsNullOrWhiteSpace(request.IqamaNumber), () =>
            {
                RuleFor(request => request.IqamaNumber!)
                    .Matches("^2[0-9]{9}$").Bilingual(
                        "The Iqama number is 10 digits and starts with 2.",
                        "رقم الإقامة مكوّن من 10 أرقام ويبدأ بالرقم 2.")
                    // Luhn checksum, mirroring the self-service path.
                    .Must(UpsertUserProfileRequestValidator.IsValidLuhn)
                    .Bilingual(
                        "The Iqama number is not a valid number.",
                        "رقم الإقامة غير صحيح.");
            });
            When(request => !string.IsNullOrWhiteSpace(request.PassportNumber), () =>
            {
                RuleFor(request => request.PassportNumber!)
                    .MaximumLength(20).Bilingual(
                        "Passport number must be at most 20 characters.",
                        "يجب ألا يتجاوز رقم جواز السفر 20 حرفًا.");
            });
        });

        // The same standard phone shapes as the self-service
        // profile upsert (UpsertUserProfileRequestValidator), separators
        // stripped first.
        When(request => !string.IsNullOrWhiteSpace(request.SaudiMobile), () =>
        {
            RuleFor(request => request.SaudiMobile!)
                .Must(value => UpsertUserProfileRequestValidator
                    .IsStandardSaudiMobile(value))
                .Bilingual(
                    "The Saudi mobile must be 05XXXXXXXX or +9665XXXXXXXX.",
                    "يجب أن يكون رقم الجوال السعودي بصيغة 05XXXXXXXX أو +9665XXXXXXXX.");
        });

        When(request => !string.IsNullOrWhiteSpace(request.InternationalMobile), () =>
        {
            RuleFor(request => request.InternationalMobile!)
                .Must(value => UpsertUserProfileRequestValidator
                    .IsStandardInternationalMobile(value))
                .Bilingual(
                    "The international mobile must be in the +<country code><number> (E.164) format.",
                    "يجب أن يكون رقم الجوال الدولي بالصيغة الدولية ‎+‎ يليها رمز الدولة والرقم (E.164).");
        });

        RuleFor(request => request.PlaceOfBirth)
            .MaximumLength(128).Bilingual(
                "Place of birth must be at most 128 characters.",
                "يجب ألا يتجاوز مكان الميلاد 128 حرفًا.");

        // Optional job title, max 100 chars (owner 2026-07-06).
        RuleFor(request => request.JobTitle)
            .MaximumLength(100).When(r => !string.IsNullOrEmpty(r.JobTitle))
            .Bilingual(
                "Job title must be at most 100 characters.",
                "يجب ألا يتجاوز المسمى الوظيفي 100 حرف.");

        // 2026-07-19 (owner) — Arabic job title twin, same 100-char cap.
        RuleFor(request => request.JobTitleArabic)
            .MaximumLength(100).When(r => !string.IsNullOrEmpty(r.JobTitleArabic))
            .Bilingual(
                "Arabic job title must be at most 100 characters.",
                "يجب ألا يتجاوز المسمى الوظيفي بالعربية 100 حرف.");

        RuleFor(request => request.InterestIds.Count)
            .LessThanOrEqualTo(10).Bilingual(
                "You can pick up to 10 interests.",
                "يمكنك اختيار حتى 10 اهتمامات.");

        // Optional plate (relaxed 2026-07-06); when present it must
        // be plate letters from the 17-letter set and/or digits (up to 3 + up
        // to 4), the same rule as the self-service profile upsert.
        RuleFor(request => request.PlateNumber)
            .Must(value => string.IsNullOrEmpty(value) || SaudiPlate.IsValid(value))
            .Bilingual(
                "Enter a valid plate: Saudi plate letters and/or digits.",
                "أدخل رقم لوحة صحيح: حروف لوحات سعودية و/أو أرقام.");

        // Gender must be a defined enum value.
        RuleFor(request => request.Gender)
            .IsInEnum().Bilingual(
                "Select a valid gender.",
                "اختر جنسًا صحيحًا.");

        // VVIP/VIP موج-integration extras. All optional; lengths
        // match UserProfile (MawjId 64, Honorific 64, PreferredLanguage 16) per
        // the validation-alignment rule.
        RuleFor(request => request.MawjId)
            .MaximumLength(64).When(r => !string.IsNullOrEmpty(r.MawjId))
            .Bilingual(
                "Mawj ID must be at most 64 characters.",
                "يجب ألا يتجاوز المعرف في نظام موج 64 حرفًا.");

        RuleFor(request => request.Honorific)
            .MaximumLength(64).When(r => !string.IsNullOrEmpty(r.Honorific))
            .Bilingual(
                "Honorific must be at most 64 characters.",
                "يجب ألا يتجاوز اللقب 64 حرفًا.");

        // 2026-07-19 (owner) — Arabic honorific twin, same 64-char cap.
        RuleFor(request => request.HonorificArabic)
            .MaximumLength(64).When(r => !string.IsNullOrEmpty(r.HonorificArabic))
            .Bilingual(
                "Arabic honorific must be at most 64 characters.",
                "يجب ألا يتجاوز اللقب بالعربية 64 حرفًا.");

        RuleFor(request => request.PreferredLanguage)
            .MaximumLength(16).When(r => !string.IsNullOrEmpty(r.PreferredLanguage))
            .Bilingual(
                "Preferred language must be at most 16 characters.",
                "يجب ألا تتجاوز اللغة المفضلة 16 حرفًا.");
    }
}
