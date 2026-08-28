// Tests: SIMF.Api.Tests/UserProfileTests.cs (the mobile is REQUIRED:
//        no-mobile-at-all and blank-an-existing-number are both 400,
//        international-only satisfies the rule for a Saudi national)
using FastEndpoints;
using FluentValidation;
using SIMF.Api.Endpoints.Auth.Validators;
using SIMF.Common;
using SIMF.Contracts.UserProfile;
using SIMF.Domain.Organisations;

namespace SIMF.Api.Endpoints.Account.Validators;

/// <summary>
/// Validates the user-profile upsert (renamed from
/// <c>UpsertVisitorProfileRequestValidator</c>). The
/// <c>VisitorType</c> string-discriminator rule was dropped: the
/// profile-type now flows through the <c>UserProfile.ProfileTypeId</c>
/// FK assigned by an admin, not a free-text claim by the user. The
/// phone rules are the strict ones, superseding earlier permissive
/// guidance: Saudi mobile <c>05XXXXXXXX</c> or <c>+9665XXXXXXXX</c>;
/// international mobile E.164 (<c>+</c> then 8–15 digits). Spaces and
/// dashes are stripped before the match, client and server identically.
///
/// <para>The nationality code shape is checked here; the
/// existence check (must resolve to a row in the <c>Country</c> table)
/// runs in <c>UserProfileService</c> against
/// <c>SimfAppDbContext.Countries</c>, since FluentValidation is sync
/// and the country table now lives in a different DbContext.</para>
/// </summary>
public sealed class UpsertUserProfileRequestValidator
    : Validator<UpsertUserProfileRequest>
{
    // The Saudi mobile standard: local 05XXXXXXXX or the
    // +9665XXXXXXXX international form of the same plan.
    private static readonly System.Text.RegularExpressions.Regex SaudiMobileShape =
        new(@"^(05\d{8}|\+9665\d{8})$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    // International mobiles must be E.164: "+", a non-zero
    // leading digit, 8–15 digits total.
    private static readonly System.Text.RegularExpressions.Regex E164Shape =
        new(@"^\+[1-9]\d{7,14}$",
            System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Saudi-mobile standard check — shared with the
    /// walk-in registration validator so the rule lives once. The separator
    /// stripping is <see cref="MobileNumber.Normalize"/>, which lives in
    /// <c>SIMF.Common</c> so the write paths canonicalise with the SAME
    /// normaliser this rule matches against, instead of a second copy.</summary>
    public static bool IsStandardSaudiMobile(string value)
        => SaudiMobileShape.IsMatch(MobileNumber.Normalize(value));

    /// <summary>E.164 international-mobile standard check — shared with
    /// the walk-in registration validator.</summary>
    public static bool IsStandardInternationalMobile(string value)
        => E164Shape.IsMatch(MobileNumber.Normalize(value));

    /// <summary>Saudi vehicle-plate standard check — restricted to
    /// the official 17-letter set (Arabic or Latin), 3 letters + 1–4 digits,
    /// either order. Delegates to the shared <see cref="SaudiPlate"/> (one
    /// source of truth, mirrored by the client's <c>plate_validation.dart</c>).</summary>
    public static bool IsStandardPlateNumber(string value)
        => SaudiPlate.IsValid(value);

    // Owner rule — the Arabic name must be Arabic letters only and the
    // English name Latin letters only (no digits, punctuation or cross-
    // script characters), and each must be a full name of at least 2 parts
    // (relaxed from a short-lived ≥4 rule). The char restriction
    // guarantees every part is in the field's language.
    //
    // The accepted class runs U+0621..U+0652: the Arabic letters and
    // tatweel (U+0640) as before, PLUS the tashkeel marks U+064B..U+0652
    // (fathatan … sukun, which includes the SHADDA U+0651). An ordinary Arabic
    // name carries a shadda — the product's own seed data does — and the old
    // U+0621..U+064A ceiling rejected it with "Arabic letters only". Arabic-Indic
    // digits (U+0660..U+0669), Latin letters and punctuation stay rejected. The
    // client mirror is `name_validation.dart` (arabicNameLettersOnly /
    // arabicNameCharacters).
    private static readonly System.Text.RegularExpressions.Regex ArabicNameShape =
        new(@"^[\u0621-\u0652\s]+$",
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

    /// <summary>Owner rule (superseding an earlier ≥4 rule) — a "full name"
    /// is at least 2 whitespace-separated parts. Splits on any whitespace
    /// (matching the name regex's <c>\s</c> and the client's <c>\s+</c>) so a tab-
    /// or NBSP-separated name counts its parts the same way everywhere. No upper
    /// cap on parts — length is bounded by MaximumLength. Empty defers to the
    /// NotEmpty rule.</summary>
    public static bool HaveAtLeastTwoParts(string? value)
        => string.IsNullOrWhiteSpace(value)
            || value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length >= 2;

    public UpsertUserProfileRequestValidator()
    {
        // ProfileTypeId is optional. Only shape-check here
        // (non-empty Guid when supplied); the existence /
        // IsActive / Visitor-scope check runs in the service against
        // the App DB (cross-context, FluentValidation is sync).
        RuleFor(request => request.ProfileTypeId)
            .Must(id => id is null || id != Guid.Empty)
            .Bilingual(
                "Profile type id is not a valid identifier.",
                "معرّف نوع الملف الشخصي غير صالح.");

        // الجهة. Required for every registrant (owner rule —
        // org mandatory across Web + App + CP). Shape-check only here
        // (non-null, non-empty Guid); the existence / IsActive check runs in
        // the service against the App DB (cross-context, FluentValidation is
        // sync), exactly like ProfileTypeId / NationalityCode.
        RuleFor(request => request.OrganisationId)
            .Must(id => id is { } orgId && orgId != Guid.Empty).Bilingual(
                "Organisation is required.",
                "الجهة مطلوبة.");

        // "Other" is a pick like any other, so the rule above is satisfied by
        // it — but picking it and typing nothing records less than picking a
        // real employer, which is the one outcome this whole path exists to
        // avoid. Demanded here rather than in the service because it is a
        // shape rule on the request, not a lookup.
        RuleFor(request => request.OrganisationOther)
            .NotEmpty()
            .When(r => r.OrganisationId == Organisation.OtherId)
            .Bilingual(
                "Enter your organisation's name.",
                "اكتب اسم جهة عملك.");

        // 150 matches the lookup's own name column, so a value promoted into
        // the list later cannot be truncated on the way in.
        RuleFor(request => request.OrganisationOther)
            .MaximumLength(150)
            .When(r => !string.IsNullOrEmpty(r.OrganisationOther))
            .Bilingual(
                "Organisation name must be at most 150 characters.",
                "يجب ألا يتجاوز اسم الجهة 150 حرفًا.");

        // المنطقة is optional. Shape-check only here (non-empty
        // Guid when supplied); the existence / IsActive check runs in the service
        // against the App DB (cross-context, FluentValidation is sync), exactly
        // like ProfileTypeId.
        RuleFor(request => request.RegionId)
            .Must(id => id is null || id != Guid.Empty).Bilingual(
                "Region id is not a valid identifier.",
                "معرّف المنطقة غير صالح.");

        // الجنس. Must be a defined enum value (Unspecified is
        // allowed — the field is optional).
        RuleFor(request => request.Gender)
            .IsInEnum()
            .Bilingual(
                "The gender selection is not valid.",
                "اختيار الجنس غير صالح.");

        // Interests are saved in the SECOND step now (profile-first
        // save), so the profile save may legitimately carry 0 interests; the
        // interests screen still requires 1-10 client-side. Server caps at 10 and
        // enforces distinct. Defence-in-depth "every id active" re-checks live on
        // the service.
        RuleFor(request => request.InterestIds)
            .Must(ids => ids is null || ids.Count <= 10)
            .Bilingual(
                "Pick at most 10 interests.",
                "اختر 10 اهتمامات كحد أقصى.")
            .Must(ids => ids is null || ids.Distinct().Count() == ids.Count)
            .Bilingual(
                "Each interest may only be picked once.",
                "لا يمكن اختيار الاهتمام أكثر من مرة.");

        RuleFor(request => request.ArabicName)
            .NotEmpty().Bilingual(
                "The Arabic name is required.",
                "الاسم بالعربية مطلوب.")
            .MaximumLength(50)
            .Must(BeArabicLettersOnly).Bilingual(
                "The Arabic name must contain Arabic letters only.",
                "يجب أن يحتوي الاسم بالعربية على حروف عربية فقط.")
            .Must(HaveAtLeastTwoParts).Bilingual(
                "Enter your full name in Arabic — at least 2 parts.",
                "أدخل اسمك الكامل بالعربية — مقطعان على الأقل.");

        RuleFor(request => request.EnglishName)
            .NotEmpty().Bilingual(
                "The English name is required.",
                "الاسم بالإنجليزية مطلوب.")
            .MaximumLength(50)
            .Must(BeEnglishLettersOnly).Bilingual(
                "The English name must contain English letters only.",
                "يجب أن يحتوي الاسم بالإنجليزية على حروف إنجليزية فقط.")
            .Must(HaveAtLeastTwoParts).Bilingual(
                "Enter your full name in English — at least 2 parts.",
                "أدخل اسمك الكامل بالإنجليزية — مقطعان على الأقل.");

        RuleFor(request => request.NationalityCode)
            .NotEmpty().Bilingual(
                "Nationality is required.",
                "الجنسية مطلوبة.")
            .Length(2, 2).Bilingual(
                "Nationality code must be 2 characters (ISO 3166-1 alpha-2).",
                "يجب أن يتكوّن رمز الجنسية من حرفين (ISO 3166-1).");

        RuleFor(request => request.PlaceOfBirth)
            .MaximumLength(128);

        // Optional job title, max 100 chars (owner 2026-07-06).
        RuleFor(request => request.JobTitle)
            .MaximumLength(100).When(r => !string.IsNullOrEmpty(r.JobTitle))
            .Bilingual(
                "Job title must be at most 100 characters.",
                "يجب ألا يتجاوز المسمى الوظيفي 100 حرف.");

        // 2026-07-20 — the Arabic twin, same 100-char SSOT as JobTitle.
        RuleFor(request => request.JobTitleArabic)
            .MaximumLength(100).When(r => !string.IsNullOrEmpty(r.JobTitleArabic))
            .Bilingual(
                "Job title (Arabic) must be at most 100 characters.",
                "يجب ألا يتجاوز المسمى الوظيفي (بالعربية) 100 حرف.");

        // Date of birth is required, and the registrant must be at
        // least 18 years old (owner rule). The age check is leap-safe via
        // DateOnly.AddYears; eligible iff dob is on or before (today − 18y).
        RuleFor(request => request.DateOfBirth)
            .NotNull().Bilingual(
                "Date of birth is required.",
                "تاريخ الميلاد مطلوب.")
            .Must(BeAtLeastEighteen).Bilingual(
                "You must be at least 18 years old to register.",
                "يجب أن يكون عمرك 18 عامًا على الأقل للتسجيل.");

        // Conditional ID fields — strict national-id prefix rules per the
        // Saudi numbering plan, plus the real Luhn check digit on top of
        // the prefix/length shape:
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

        // The mobile is REQUIRED. It was mandatory on the app form and on the
        // walk-in desk but optional here, so a save could still CLEAR the number
        // the app then refuses to let the user submit without. The rule is the
        // walk-in desk's, word for word: at least one mobile, Saudi or
        // international — which is what the app always sends (exactly one, chosen
        // by IsSaudi) and what keeps a Saudi national reachable on a foreign
        // number.
        RuleFor(request => request)
            .Must(r => !string.IsNullOrWhiteSpace(r.SaudiMobile)
                || !string.IsNullOrWhiteSpace(r.InternationalMobile))
            .Bilingual(
                "A mobile number is required (Saudi or international).",
                "رقم الجوال مطلوب (سعودي أو دولي).");

        // Standard shapes, separators stripped first.
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

        // رقم اللوحة: optional (relaxed 2026-07-06), but when
        // present it must be plate letters from the official 17-letter set
        // (Arabic or Latin) and/or digits — up to 3 letters + up to 4 digits,
        // at least one of them.
        RuleFor(request => request.PlateNumber)
            .Must(value => string.IsNullOrEmpty(value) || IsStandardPlateNumber(value))
            .Bilingual(
                "Enter a valid plate: Saudi plate letters and/or digits.",
                "أدخل رقم لوحة صحيح: حروف لوحات سعودية و/أو أرقام.");
    }

    // The registrant must be at least 18. "Today" is the SAUDI day: this
    // read DateTime.UtcNow until 2026-08-01, and that clock is still on the previous
    // date until 03:00 Riyadh, so anyone registering in those first three hours
    // of their 18th birthday was told they were too young. Returns true for null
    // (the NotNull rule owns the required-message).
    private static bool BeAtLeastEighteen(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is null) { return true; }
        var today = DateOnly.FromDateTime(SimfClock.Now);
        return dateOfBirth.Value <= today.AddYears(-18);
    }

    // Standard Luhn mod-10 over all digits (the last is the check
    // digit). Saudi national ids and Iqama numbers are Luhn-valid; this is
    // the real check on top of the prefix/length regex. Public so the walk-in
    // validator reuses the same check.
    //
    // The implementation moved to SIMF.Common.IdentityDocument so the
    // offline badge upload can apply it too: that path runs in
    // SIMF.Infrastructure, which cannot reference SIMF.Api, and it calls the
    // registration service directly so no validator ever executes on it. This
    // stays as a delegation rather than being deleted, because eight call sites
    // in this assembly already use it and a rename would be churn for nothing.
    public static bool IsValidLuhn(string number) =>
        IdentityDocument.IsValidLuhn(number);
}
