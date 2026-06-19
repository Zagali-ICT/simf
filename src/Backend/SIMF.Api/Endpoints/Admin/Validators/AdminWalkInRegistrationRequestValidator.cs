using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Account.Validators;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Common;
using SIMF.Contracts.Authentication;

namespace SIMF.Api.Endpoints.Admin.Validators;

/// <summary>
/// D-127 — validates the walk-in registration request. Email is optional
/// (the desk frequently registers walk-ins without one); the matching
/// service synthesizes a placeholder. Every other field that the badge /
/// profile needs is required — staff at the desk is collecting them
/// face-to-face, so the validator pushes back on incomplete submissions
/// rather than silently storing nulls.
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
            .NotEmpty().Bilingual(
                "Display name is required.",
                "الاسم المعروض مطلوب.")
            .MinimumLength(2).Bilingual(
                "Display name must be at least 2 characters.",
                "يجب أن يتكوّن الاسم المعروض من حرفين على الأقل.")
            .MaximumLength(128).Bilingual(
                "Display name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم المعروض 128 حرفًا.");

        RuleFor(request => request.ArabicName)
            .NotEmpty().Bilingual(
                "Arabic name is required.",
                "الاسم بالعربية مطلوب.")
            .MaximumLength(128).Bilingual(
                "Arabic name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالعربية 128 حرفًا.");

        RuleFor(request => request.EnglishName)
            .NotEmpty().Bilingual(
                "English name is required.",
                "الاسم بالإنجليزية مطلوب.")
            .MaximumLength(128).Bilingual(
                "English name must be at most 128 characters.",
                "يجب ألا يتجاوز الاسم بالإنجليزية 128 حرفًا.");

        RuleFor(request => request.ProfileTypeId)
            .NotEqual(Guid.Empty).Bilingual(
                "Profile type is required.",
                "نوع الملف الشخصي مطلوب.");

        // B3 — D-221 (الجهة): required at the desk. Shape-check only here
        // (non-null, non-empty Guid); the existence / IsActive check runs in
        // the service against the App DB (cross-context — FluentValidation is
        // sync), exactly like ProfileTypeId / NationalityCode.
        RuleFor(request => request.OrganisationId)
            .Must(id => id is { } orgId && orgId != Guid.Empty).Bilingual(
                "Organisation is required.",
                "الجهة مطلوبة.");

        RuleFor(request => request.NationalityCode)
            .NotEmpty().Bilingual(
                "Nationality is required.",
                "الجنسية مطلوبة.")
            .Length(2, 3).Bilingual(
                "Nationality code must be 2-3 characters (ISO 3166-1).",
                "يجب أن يتكوّن رمز الجنسية من 2-3 أحرف (ISO 3166-1).");

        When(request => request.IsSaudi, () =>
        {
            RuleFor(request => request.NationalId)
                .NotEmpty().Bilingual(
                    "Saudi national ID is required for Saudi nationals.",
                    "الهوية الوطنية مطلوبة للمواطنين السعوديين.")
                .Matches("^1[0-9]{9}$").Bilingual(
                    "The Saudi national ID is 10 digits and starts with 1.",
                    "الهوية الوطنية السعودية مكوّنة من 10 أرقام وتبدأ بالرقم 1.")
                // D-459 — apply the same Luhn checksum the self-service path
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
            RuleFor(request => request)
                .Must(r => !string.IsNullOrWhiteSpace(r.IqamaNumber)
                    || !string.IsNullOrWhiteSpace(r.PassportNumber))
                .Bilingual(
                    "An Iqama or passport number is required.",
                    "رقم الإقامة أو جواز السفر مطلوب.");
            When(request => !string.IsNullOrWhiteSpace(request.IqamaNumber), () =>
            {
                RuleFor(request => request.IqamaNumber!)
                    .Matches("^2[0-9]{9}$").Bilingual(
                        "The Iqama number is 10 digits and starts with 2.",
                        "رقم الإقامة مكوّن من 10 أرقام ويبدأ بالرقم 2.")
                    // D-459 — Luhn checksum, mirroring the self-service path.
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

        // At least one phone — Saudi or international — so the desk can
        // reach the visitor afterwards.
        RuleFor(request => request)
            .Must(r => !string.IsNullOrWhiteSpace(r.SaudiMobile)
                || !string.IsNullOrWhiteSpace(r.InternationalMobile))
            .Bilingual(
                "A mobile number is required (Saudi or international).",
                "رقم الجوال مطلوب (سعودي أو دولي).");

        // C4 (D-371) — the same standard phone shapes as the self-service
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

        // D-163 (PDF §2.6) — optional job title, max 128 chars.
        RuleFor(request => request.JobTitle)
            .MaximumLength(128).When(r => !string.IsNullOrEmpty(r.JobTitle))
            .Bilingual(
                "Job title must be at most 128 characters.",
                "يجب ألا يتجاوز المسمى الوظيفي 128 حرفًا.");

        RuleFor(request => request.InterestIds.Count)
            .LessThanOrEqualTo(10).Bilingual(
                "You can pick up to 10 interests.",
                "يمكنك اختيار حتى 10 اهتمامات.");

        // C6 (D-459) — optional plate; when present it must match the Saudi
        // standard (17-letter set + 1–4 digits), the same rule as the
        // self-service profile upsert (no longer just a length cap).
        RuleFor(request => request.PlateNumber)
            .Must(value => string.IsNullOrEmpty(value) || SaudiPlate.IsValid(value))
            .Bilingual(
                "The plate number must be 3 letters (Saudi plate set) and up to 4 digits.",
                "يجب أن يتكوّن رقم اللوحة من 3 أحرف (من حروف اللوحات السعودية) وحتى 4 أرقام.");

        // D-395 — gender must be a defined enum value.
        RuleFor(request => request.Gender)
            .IsInEnum().Bilingual(
                "Select a valid gender.",
                "اختر جنسًا صحيحًا.");

        // V-1 (D-429) — VVIP/VIP موج-integration extras. All optional; lengths
        // match UserProfile (MawjId 64, Honorific 64, PreferredLanguage 16) per
        // the validation-alignment rule (SES §7).
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

        RuleFor(request => request.PreferredLanguage)
            .MaximumLength(16).When(r => !string.IsNullOrEmpty(r.PreferredLanguage))
            .Bilingual(
                "Preferred language must be at most 16 characters.",
                "يجب ألا تتجاوز اللغة المفضلة 16 حرفًا.");
    }
}
