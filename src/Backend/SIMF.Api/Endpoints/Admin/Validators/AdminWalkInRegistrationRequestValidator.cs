using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
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
                .Length(10).Bilingual(
                    "The Saudi national ID is 10 digits.",
                    "الهوية الوطنية السعودية مكوّنة من 10 أرقام.");
        }).Otherwise(() =>
        {
            // Non-Saudi: either Iqama OR Passport must be supplied.
            RuleFor(request => request)
                .Must(r => !string.IsNullOrWhiteSpace(r.IqamaNumber)
                    || !string.IsNullOrWhiteSpace(r.PassportNumber))
                .Bilingual(
                    "An Iqama or passport number is required.",
                    "رقم الإقامة أو جواز السفر مطلوب.");
        });

        // At least one phone — Saudi or international — so the desk can
        // reach the visitor afterwards.
        RuleFor(request => request)
            .Must(r => !string.IsNullOrWhiteSpace(r.SaudiMobile)
                || !string.IsNullOrWhiteSpace(r.InternationalMobile))
            .Bilingual(
                "A mobile number is required (Saudi or international).",
                "رقم الجوال مطلوب (سعودي أو دولي).");

        RuleFor(request => request.PlaceOfBirth)
            .MaximumLength(128).Bilingual(
                "Place of birth must be at most 128 characters.",
                "يجب ألا يتجاوز مكان الميلاد 128 حرفًا.");

        RuleFor(request => request.InterestIds.Count)
            .LessThanOrEqualTo(10).Bilingual(
                "You can pick up to 10 interests.",
                "يمكنك اختيار حتى 10 اهتمامات.");
    }
}
