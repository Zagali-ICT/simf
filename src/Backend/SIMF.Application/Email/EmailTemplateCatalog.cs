using SIMF.Common.Enums;

namespace SIMF.Application.Email;

/// <summary>D-735 — one editable placeholder a template exposes: its
/// <see cref="Name"/> (used as <c>{Name}</c>), bilingual display labels for the
/// CP editor's token chips, and a <see cref="Sample"/> value used to render the
/// live preview.</summary>
public sealed record EmailTemplateToken(
    string Name, string DisplayEn, string DisplayAr, string Sample);

/// <summary>D-735 — the code-owned default for one transactional email: subject +
/// bilingual body templates plus the tokens the body may reference. This is the
/// always-present fallback the resolver uses when no active DB override exists,
/// and the source of the CP editor's token chips, preview samples, and
/// unknown-token validation.</summary>
public sealed record EmailTemplateDefinition(
    EmailTemplateType Type,
    string Subject,
    string BodyEn,
    string BodyAr,
    IReadOnlyList<EmailTemplateToken> Tokens);

/// <summary>D-735 — the fixed catalogue of transactional-email defaults. Mirrors
/// how the AI-prompt catalogue (and the V10 notification-template catalogue)
/// works: the code owns the defaults, the DB owns only admin overrides. The
/// subject / body strings reproduce the original hard-coded bilingual copy (the
/// retired <c>TransactionalEmail</c> helper), now expressed as {Token}
/// templates.</summary>
public static class EmailTemplateCatalog
{
    private static readonly EmailTemplateToken CodeToken =
        new("Code", "One-time code", "الرمز لمرة واحدة", "123456");

    private static readonly EmailTemplateToken ExpiryToken =
        new("ExpiryMinutes", "Expiry (minutes)", "مدة الصلاحية (بالدقائق)", "10");

    private static readonly IReadOnlyList<EmailTemplateToken> CodeTokens =
        [CodeToken, ExpiryToken];

    // D-751 — tokens for the bulk-badge cover note. No one-time code: the badges
    // themselves ride the message as a ZIP attachment.
    private static readonly EmailTemplateToken CountToken =
        new("Count", "Badge count", "عدد الشارات", "8");

    private static readonly EmailTemplateToken GeneratedAtToken =
        new("GeneratedAt", "Generated at (Saudi time)", "تاريخ التوليد (بتوقيت السعودية)", "2026-07-20 09:30 AM");

    private static readonly IReadOnlyList<EmailTemplateToken> BulkBadgeTokens =
        [CountToken, GeneratedAtToken];

    // #24 — the masked new address on the change-email security alert to the old
    // address. No one-time code (the change already happened).
    private static readonly EmailTemplateToken NewEmailToken =
        new("NewEmail", "New email (masked)", "البريد الجديد (مقنّع)", "n***@example.com");

    private static readonly IReadOnlyList<EmailTemplateToken> NewEmailTokens =
        [NewEmailToken];

    // BUG-024 — the lead card emailed to the exhibitor after a booth badge scan.
    // Each displayed field is a bilingual PAIR so the English block renders the
    // English value and the Arabic block the Arabic one (the service falls the
    // other way round, then to a "not provided" placeholder, when one is missing).
    // Deliberately absent: the national ID (encrypted at rest, never emailed) and
    // the raw badge QR id (no existing template carries one).
    private static readonly IReadOnlyList<EmailTemplateToken> ExhibitorLeadTokens =
    [
        new("VisitorName", "Visitor name", "اسم الزائر", "Sara Al-Otaibi"),
        new("VisitorNameArabic", "Visitor name (Arabic)", "اسم الزائر (بالعربية)", "سارة العتيبي"),
        new("JobTitle", "Job title", "المسمى الوظيفي", "Operations Manager"),
        new("JobTitleArabic", "Job title (Arabic)", "المسمى الوظيفي (بالعربية)", "مدير العمليات"),
        new("Organisation", "Organisation", "جهة العمل", "Red Sea Shipping"),
        new("OrganisationArabic", "Organisation (Arabic)", "جهة العمل (بالعربية)", "الشحن البحري الأحمر"),
        new("ScannedAt", "Scanned at (Saudi time)", "وقت المسح (بتوقيت السعودية)", "20-07-2026 09:30 AM"),
        new("Note", "Your note", "ملاحظتك", "Interested in the fleet package"),
    ];

    private static readonly IReadOnlyList<EmailTemplateDefinition> Definitions =
    [
        new(EmailTemplateType.SignInOtp,
            "SIMF sign-in code",
            "<p>Your SIMF sign-in code is <strong>{Code}</strong>.</p>" +
            "<p>The code expires in {ExpiryMinutes} minutes.</p>",
            "<p>رمز تسجيل الدخول الخاص بك هو <strong>{Code}</strong>.</p>" +
            "<p>ينتهي الرمز خلال {ExpiryMinutes} دقائق.</p>",
            CodeTokens),

        new(EmailTemplateType.EmailVerification,
            "SIMF email verification",
            "<p>Your SIMF email verification code is <strong>{Code}</strong>.</p>" +
            "<p>The code expires in {ExpiryMinutes} minutes.</p>",
            "<p>رمز التحقق من بريدك الإلكتروني هو <strong>{Code}</strong>.</p>" +
            "<p>ينتهي الرمز خلال {ExpiryMinutes} دقائق.</p>",
            CodeTokens),

        new(EmailTemplateType.AccountExists,
            "SIMF account already exists",
            "<p>Someone tried to create a SIMF account with this email address, " +
            "but an account already exists.</p>" +
            "<p>If this was you, please sign in instead — or use the " +
            "&quot;forgot password&quot; option if you don't remember your password.</p>" +
            "<p>If this wasn't you, you can safely ignore this email.</p>",
            "<p>حاول أحدهم إنشاء حساب بهذا البريد الإلكتروني، لكن يوجد حساب مسجَّل بالفعل.</p>" +
            "<p>إن كان ذلك أنت، فسجّل الدخول بدلاً من ذلك — أو استخدم خيار «نسيت كلمة المرور» إن لم تتذكّرها.</p>" +
            "<p>إن لم يكن ذلك أنت، فيمكنك تجاهل هذه الرسالة بأمان.</p>",
            []),

        new(EmailTemplateType.PasswordReset,
            "SIMF password reset",
            "<p>Your SIMF password reset code is <strong>{Code}</strong>.</p>" +
            "<p>The code expires in {ExpiryMinutes} minutes. " +
            "If you did not request a password reset, you can ignore this email.</p>",
            "<p>رمز إعادة تعيين كلمة المرور الخاص بك هو <strong>{Code}</strong>.</p>" +
            "<p>ينتهي الرمز خلال {ExpiryMinutes} دقائق. " +
            "إذا لم تطلب إعادة تعيين كلمة المرور فيمكنك تجاهل هذه الرسالة.</p>",
            CodeTokens),

        new(EmailTemplateType.BadgeActivation,
            "SIMF account activation",
            "<p>Your SIMF account activation code is <strong>{Code}</strong>.</p>" +
            "<p>The code expires in {ExpiryMinutes} minutes. " +
            "Enter it in the app to set your password.</p>",
            "<p>رمز تفعيل حسابك هو <strong>{Code}</strong>.</p>" +
            "<p>ينتهي الرمز خلال {ExpiryMinutes} دقائق. أدخله في التطبيق لتعيين كلمة المرور.</p>",
            CodeTokens),

        new(EmailTemplateType.BiometricStepUp,
            "SIMF biometric sign-in code",
            "<p>Your SIMF biometric sign-in confirmation code is <strong>{Code}</strong>.</p>" +
            "<p>The code expires in {ExpiryMinutes} minutes. " +
            "If you did not request to enable Face-ID sign-in, ignore this email.</p>",
            "<p>رمز تأكيد تفعيل تسجيل الدخول ببصمة الوجه هو <strong>{Code}</strong>.</p>" +
            "<p>ينتهي الرمز خلال {ExpiryMinutes} دقائق. " +
            "إذا لم تطلب تفعيل تسجيل الدخول ببصمة الوجه فتجاهل هذه الرسالة.</p>",
            CodeTokens),

        new(EmailTemplateType.BulkBadgeDelivery,
            "Your SIMF badge batch ({Count} badges)",
            "<p>A batch of {Count} SIMF badge(s) was generated for you on {GeneratedAt}.</p>" +
            "<p>The QR badge images are attached to this email as a single ZIP file, " +
            "one PNG per badge.</p>",
            "<p>تم توليد دفعة من {Count} شارة في نظام سيمف بتاريخ {GeneratedAt}.</p>" +
            "<p>صور رموز QR للشارات مرفقة بهذه الرسالة في ملف مضغوط واحد، صورة PNG لكل شارة.</p>",
            BulkBadgeTokens),

        new(EmailTemplateType.EmailChangeVerification,
            "SIMF email change verification",
            "<p>Your SIMF email change verification code is <strong>{Code}</strong>.</p>" +
            "<p>The code expires in {ExpiryMinutes} minutes. Enter it in the app to " +
            "confirm this is your new email address. " +
            "If you did not request an email change, ignore this message.</p>",
            "<p>رمز تأكيد تغيير البريد الإلكتروني الخاص بك هو <strong>{Code}</strong>.</p>" +
            "<p>ينتهي الرمز خلال {ExpiryMinutes} دقائق. أدخله في التطبيق لتأكيد أن هذا " +
            "بريدك الإلكتروني الجديد. " +
            "إذا لم تطلب تغيير البريد الإلكتروني فتجاهل هذه الرسالة.</p>",
            CodeTokens),

        new(EmailTemplateType.EmailChangedNotice,
            "SIMF login email changed",
            "<p>The login email for your SIMF account was just changed to " +
            "<strong>{NewEmail}</strong>.</p>" +
            "<p>If you made this change, no action is needed. If you did NOT change " +
            "it, your account may be compromised — contact SIMF support immediately.</p>",
            "<p>تم تغيير البريد الإلكتروني لتسجيل الدخول إلى حسابك في سيمف إلى " +
            "<strong>{NewEmail}</strong>.</p>" +
            "<p>إذا كنت أنت من أجرى هذا التغيير فلا حاجة لأي إجراء. وإذا لم تكن أنت، " +
            "فقد يكون حسابك معرّضاً للخطر — تواصل مع دعم سيمف فوراً.</p>",
            NewEmailTokens),

        new(EmailTemplateType.ExhibitorLeadCapture,
            "SIMF visitor captured at your booth: {VisitorName}",
            "<p>You scanned a visitor badge at your booth. The visitor was added to " +
            "your <strong>My Booth Visitors</strong> list in the SIMF app.</p>" +
            "<p><strong>{VisitorName}</strong><br/>{JobTitle}<br/>{Organisation}</p>" +
            "<p>Scanned at {ScannedAt} (Saudi time).<br/>Your note: {Note}</p>" +
            "<p>The full card, with the visitor's contact details, is in " +
            "<strong>My Booth Visitors</strong> in the app.</p>",
            "<p>قمت بمسح بطاقة زائر في جناحك، وتمت إضافته إلى قائمة " +
            "<strong>زوار جناحي</strong> في تطبيق سيمف.</p>" +
            "<p><strong>{VisitorNameArabic}</strong><br/>{JobTitleArabic}<br/>{OrganisationArabic}</p>" +
            "<p>وقت المسح {ScannedAt} (بتوقيت السعودية).<br/>ملاحظتك: {Note}</p>" +
            "<p>البطاقة الكاملة مع بيانات التواصل متاحة في <strong>زوار جناحي</strong> " +
            "داخل التطبيق.</p>",
            ExhibitorLeadTokens),
    ];

    private static readonly IReadOnlyDictionary<EmailTemplateType, EmailTemplateDefinition> Map =
        Definitions.ToDictionary(d => d.Type);

    /// <summary>Every transactional-email type, in catalogue order.</summary>
    public static IReadOnlyList<EmailTemplateDefinition> All => Definitions;

    /// <summary>The code-owned default for a type. Always present.</summary>
    public static EmailTemplateDefinition Default(EmailTemplateType type) => Map[type];

    /// <summary>Like <see cref="Default"/> but never throws: an unmapped type
    /// (e.g. a future enum value appended without a catalogue entry) degrades to
    /// the first definition instead of a <see cref="KeyNotFoundException"/>. The
    /// resolver uses this so its never-throw guarantee holds even for a mis-wired
    /// new template type.</summary>
    public static EmailTemplateDefinition DefaultOrFallback(EmailTemplateType type) =>
        Map.TryGetValue(type, out var definition) ? definition : Definitions[0];

    /// <summary>The token names a type's body may reference (case-insensitive).
    /// Anything else in a saved template is an unknown token.</summary>
    public static ISet<string> KnownTokenNames(EmailTemplateType type) =>
        Map[type].Tokens
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
