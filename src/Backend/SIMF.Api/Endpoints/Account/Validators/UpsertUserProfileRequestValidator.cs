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
/// phone rules follow the C4 standard (D-371, superseding the permissive
/// D-046 guidance): Saudi mobile <c>05XXXXXXXX</c> or <c>+9665XXXXXXXX</c>;
/// international mobile E.164 (<c>+</c> then 8–15 digits). Spaces and
/// dashes are stripped before the match, client and server identically.
///
/// <para>D-151 — the nationality code shape is checked here; the
/// existence check (must resolve to a row in the <c>Country</c> table)
/// runs in <c>UserProfileService</c> against
/// <c>SimfAppDbContext.Countries</c>, since FluentValidation is sync
/// and the country table now lives in a different DbContext.</para>
/// </summary>
public sealed class UpsertUserProfileRequestValidator
    : Validator<UpsertUserProfileRequest>
{
    // C4 (D-371) — the Saudi mobile standard: local 05XXXXXXXX or the
    // +9665XXXXXXXX international form of the same plan.
    private static readonly System.Text.RegularExpressions.Regex SaudiMobileShape =
        new(@"^(05\d{8}|\+9665\d{8})$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    // C4 (D-371) — international mobiles must be E.164: "+", a non-zero
    // leading digit, 8–15 digits total.
    private static readonly System.Text.RegularExpressions.Regex E164Shape =
        new(@"^\+[1-9]\d{7,14}$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Strips the separators users habitually type (spaces and
    /// dashes) so "+9665 0123-4567" and "+966501234567" validate alike.</summary>
    private static string NormalizePhone(string value)
        => value.Replace(" ", string.Empty).Replace("-", string.Empty);

    /// <summary>C4 (D-371) Saudi-mobile standard check — shared with the
    /// walk-in registration validator so the rule lives once.</summary>
    public static bool IsStandardSaudiMobile(string value)
        => SaudiMobileShape.IsMatch(NormalizePhone(value.Trim()));

    /// <summary>C4 (D-371) E.164 international-mobile standard check —
    /// shared with the walk-in registration validator.</summary>
    public static bool IsStandardInternationalMobile(string value)
        => E164Shape.IsMatch(NormalizePhone(value.Trim()));

    /// <summary>C6 (D-459) Saudi vehicle-plate standard check — restricted to
    /// the official 17-letter set (Arabic or Latin), 3 letters + 1–4 digits,
    /// either order. Delegates to the shared <see cref="SaudiPlate"/> (one
    /// source of truth, mirrored by the client's <c>plate_validation.dart</c>).</summary>
    public static bool IsStandardPlateNumber(string value)
        => SaudiPlate.IsValid(value);

    // Owner rule — the Arabic name must be Arabic letters only and the
    // English name Latin letters only (no digits, punctuation or cross-
    // script characters), and each must be a full name of 2 to 4 parts
    // (D-459, was ≥4). The char restriction guarantees every part is in the
    // field's language, so "2–4 parts, same language" reduces to these checks.
    private static readonly System.Text.RegularExpressions.Regex ArabicNameShape =
        new(@"^[ء-ي\s]+$",
            System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex EnglishNameShape =
        new(@"^[A-Za-z\s]+$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>True when the value is Arabic letters + spaces only. Returns
    /// true for empty (the NotEmpty rule owns the required message, so we
    /// don't double-report). Public so the walk-in validator reuses the rule.</summary>
    public static bool BeArabicLettersOnly(string? value)
        => string.IsNullOrWhiteSpace(value) || ArabicNameShape.IsMatch(value.Trim());

    /// <summary>True when the value is Latin letters + spaces only (empty
    /// defers to NotEmpty). Public so the walk-in validator reuses the rule.</summary>
    public static bool BeEnglishLettersOnly(string? value)
        => string.IsNullOrWhiteSpace(value) || EnglishNameShape.IsMatch(value.Trim());

    /// <summary>Owner rule (D-459) — a "full name" is 2 to 4 whitespace-
    /// separated parts (was "at least 4"). Splits on any whitespace (matching
    /// the name regex's <c>\s</c> and the client's <c>\s+</c>) so a tab- or
    /// NBSP-separated name counts its parts the same way everywhere. Empty
    /// defers to the NotEmpty rule.</summary>
    public static bool HaveTwoToFourParts(string? value)
        => string.IsNullOrWhiteSpace(value)
            || value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length is >= 2 and <= 4;

    public UpsertUserProfileRequestValidator()
    {
        // D-190 — ProfileTypeId is optional. Only shape-check here
        // (non-empty Guid when supplied); the existence /
        // IsActive / Visitor-scope check runs in the service against
        // the App DB (cross-context, FluentValidation is sync).
        RuleFor(request => request.ProfileTypeId)
            .Must(id => id is null || id != Guid.Empty)
            .Bilingual(
                "Profile type id is not a valid identifier.",
                "معرّف نوع الملف الشخصي غير صالح.");

        // B3 — D-221: الجهة. Required for every registrant (owner rule —
        // org mandatory across Web + App + CP). Shape-check only here
        // (non-null, non-empty Guid); the existence / IsActive check runs in
        // the service against the App DB (cross-context, FluentValidation is
        // sync), exactly like ProfileTypeId / NationalityCode.
        RuleFor(request => request.OrganisationId)
            .Must(id => id is { } orgId && orgId != Guid.Empty).Bilingual(
                "Organisation is required.",
                "الجهة مطلوبة.");

        // D-611 (Wave B) — المنطقة is optional. Shape-check only here (non-empty
        // Guid when supplied); the existence / IsActive check runs in the service
        // against the App DB (cross-context, FluentValidation is sync), exactly
        // like ProfileTypeId.
        RuleFor(request => request.RegionId)
            .Must(id => id is null || id != Guid.Empty).Bilingual(
                "Region id is not a valid identifier.",
                "معرّف المنطقة غير صالح.");

        // B3 — D-221: الجنس. Must be a defined enum value (Unspecified is
        // allowed — the field is optional).
        RuleFor(request => request.Gender)
            .IsInEnum()
            .Bilingual(
                "The gender selection is not valid.",
                "اختيار الجنس غير صالح.");

        // P9 — interests are required (min 1, max 10). Defence-in-depth
        // re-checks of "every id active" live on the service.
        RuleFor(request => request.InterestIds)
            .NotNull().Bilingual(
                "At least one interest is required.",
                "يجب اختيار اهتمام واحد على الأقل.")
            .Must(ids => ids is not null && ids.Count is >= 1 and <= 10)
            .Bilingual(
                "Pick between 1 and 10 interests.",
                "اختر ما بين 1 و 10 اهتمامات.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .Bilingual(
                "Each interest may only be picked once.",
                "لا يمكن اختيار الاهتمام أكثر من مرة.");

        RuleFor(request => request.ArabicName)
            .NotEmpty().Bilingual(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.")
            .MaximumLength(256)
            .Must(BeArabicLettersOnly).Bilingual(
                "The Arabic name must contain Arabic letters only.",
                "يجب أن يحتوي الاسم بالعربية على حروف عربية فقط.")
            .Must(HaveTwoToFourParts).Bilingual(
                "Enter your full name in Arabic — 2 to 4 parts.",
                "أدخل اسمك الكامل بالعربية — من مقطعين إلى أربعة مقاطع.");

        RuleFor(request => request.EnglishName)
            .NotEmpty().Bilingual(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.")
            .MaximumLength(256)
            .Must(BeEnglishLettersOnly).Bilingual(
                "The English name must contain English letters only.",
                "يجب أن يحتوي الاسم بالإنجليزية على حروف إنجليزية فقط.")
            .Must(HaveTwoToFourParts).Bilingual(
                "Enter your full name in English — 2 to 4 parts.",
                "أدخل اسمك الكامل بالإنجليزية — من مقطعين إلى أربعة مقاطع.");

        RuleFor(request => request.NationalityCode)
            .NotEmpty().Bilingual(
                "Nationality is required.",
                "الجنسية مطلوبة.")
            .Length(2, 2).Bilingual(
                "Nationality code must be 2 characters (ISO 3166-1 alpha-2).",
                "يجب أن يتكوّن رمز الجنسية من حرفين (ISO 3166-1).");

        RuleFor(request => request.PlaceOfBirth)
            .MaximumLength(128);

        // D-163 (PDF §2.6) — optional job title, max 128 chars.
        RuleFor(request => request.JobTitle)
            .MaximumLength(128).When(r => !string.IsNullOrEmpty(r.JobTitle))
            .Bilingual(
                "Job title must be at most 128 characters.",
                "يجب ألا يتجاوز المسمى الوظيفي 128 حرفًا.");

        // D-197 — date of birth is required, and the registrant must be at
        // least 18 years old (owner rule). The age check is leap-safe via
        // DateOnly.AddYears; eligible iff dob is on or before (today − 18y).
        RuleFor(request => request.DateOfBirth)
            .NotNull().Bilingual(
                "Date of birth is required.",
                "تاريخ الميلاد مطلوب.")
            .Must(BeAtLeastEighteen).Bilingual(
                "You must be at least 18 years old to register.",
                "يجب أن يكون عمرك 18 عامًا على الأقل للتسجيل.");

        // Conditional ID fields (P5 hardening — strict national-id prefix
        // rules per the Saudi numbering plan; D-197 adds the real Luhn
        // check digit on top of the prefix/length shape):
        //   - Saudi national id is exactly 10 digits, starts with 1, and
        //     passes the standard Luhn mod-10 checksum.
        //   - Iqama (residency permit) is exactly 10 digits, starts with 2,
        //     and passes Luhn.
        //   - Non-Saudi users who don't hold an Iqama carry a passport
        //     (6-9 letters/digits — a format sanity check; passports have
        //     no universal checksum).
        When(request => request.IsSaudi, () =>
        {
            RuleFor(r => r.NationalId)
                .NotEmpty().Bilingual(
                    "The Saudi national id is required.",
                    "رقم الهوية الوطنية مطلوب.")
                .Matches(@"^1\d{9}$").Bilingual(
                    "The Saudi national id must be 10 digits starting with 1.",
                    "يجب أن يتكوّن رقم الهوية الوطنية من 10 أرقام تبدأ بالرقم 1.")
                .Must(id => string.IsNullOrEmpty(id) || IsValidLuhn(id)).Bilingual(
                    "The Saudi national id is not a valid number.",
                    "رقم الهوية الوطنية غير صحيح.");
        });

        When(request => !request.IsSaudi, () =>
        {
            RuleFor(r => r.IqamaNumber)
                .Matches(@"^2\d{9}$").When(r => !string.IsNullOrEmpty(r.IqamaNumber))
                .Bilingual(
                    "An Iqama number must be 10 digits starting with 2.",
                    "يجب أن يتكوّن رقم الإقامة من 10 أرقام تبدأ بالرقم 2.")
                .Must(id => string.IsNullOrEmpty(id) || IsValidLuhn(id)).Bilingual(
                    "The Iqama number is not a valid number.",
                    "رقم الإقامة غير صحيح.");

            RuleFor(r => r.PassportNumber)
                .Matches(@"^[A-Za-z0-9]{6,9}$").When(r => !string.IsNullOrEmpty(r.PassportNumber))
                .Bilingual(
                    "Passport number must be 6 to 9 letters or digits.",
                    "يجب أن يتكوّن رقم جواز السفر من 6 إلى 9 أحرف أو أرقام.");

            RuleFor(r => r)
                .Must(r => !string.IsNullOrEmpty(r.IqamaNumber)
                    || !string.IsNullOrEmpty(r.PassportNumber))
                .Bilingual(
                    "Either Iqama or Passport number is required.",
                    "يجب إدخال رقم الإقامة أو رقم جواز السفر.");
        });

        // C4 (D-371) — standard shapes, separators stripped first.
        RuleFor(request => request.SaudiMobile)
            .Must(value => string.IsNullOrEmpty(value) || IsStandardSaudiMobile(value))
            .Bilingual(
                "The Saudi mobile must be 05XXXXXXXX or +9665XXXXXXXX.",
                "يجب أن يكون رقم الجوال السعودي بصيغة 05XXXXXXXX أو +9665XXXXXXXX.");

        RuleFor(request => request.InternationalMobile)
            .Must(value => string.IsNullOrEmpty(value) || IsStandardInternationalMobile(value))
            .Bilingual(
                "The international mobile must be in the +<country code><number> (E.164) format.",
                "يجب أن يكون رقم الجوال الدولي بالصيغة الدولية ‎+‎ يليها رمز الدولة والرقم (E.164).");

        // C6 (D-459) — رقم اللوحة: optional, but when present it must match
        // the Saudi standard — 3 letters from the official 17-letter set
        // (Arabic or Latin) + 1–4 digits.
        RuleFor(request => request.PlateNumber)
            .Must(value => string.IsNullOrEmpty(value) || IsStandardPlateNumber(value))
            .Bilingual(
                "The plate number must be 3 letters (Saudi plate set) and up to 4 digits.",
                "يجب أن يتكوّن رقم اللوحة من 3 أحرف (من حروف اللوحات السعودية) وحتى 4 أرقام.");
    }

    // D-197 — the registrant must be at least 18. Uses UtcNow date-only;
    // min-age is not timing-sensitive to the second. Returns true for null
    // (the NotNull rule owns the required-message).
    private static bool BeAtLeastEighteen(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is null) { return true; }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return dateOfBirth.Value <= today.AddYears(-18);
    }

    // D-197 — standard Luhn mod-10 over all digits (the last is the check
    // digit). Saudi national ids and Iqama numbers are Luhn-valid; this is
    // the real check on top of the prefix/length regex. Assumes the input
    // has already passed the digits-only regex but stays defensive. Public
    // so the walk-in validator reuses the same check (D-459).
    public static bool IsValidLuhn(string number)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var i = number.Length - 1; i >= 0; i--)
        {
            var c = number[i];
            if (c < '0' || c > '9') { return false; }
            var digit = c - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9) { digit -= 9; }
            }
            sum += digit;
            doubleDigit = !doubleDigit;
        }
        return sum % 10 == 0;
    }
}
