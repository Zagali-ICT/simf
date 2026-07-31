namespace SIMF.Contracts.Account;

/// <summary>
/// `accessibility-server-sync` — the account-level accessibility choices, the
/// server half of the app's accessibility screen (Page 038; Figma 1116:16630).
/// The five flags used to live in device prefs ONLY, so they did not follow the
/// user to a second device and did not survive a reinstall; they are now written
/// through on every change and read back at sign-in over
/// <c>GET</c> / <c>PUT /api/v1/app/account/preferences</c>
/// (<c>RequireApprovedAccount</c> — an app-user surface, no admin permission).
///
/// <para>The shipped app decodes this shape field by field and falls back to the
/// default on anything missing, so the JSON field names are a <b>frozen wire
/// contract</b>: <c>textSize</c>, <c>highContrast</c>, <c>reduceMotion</c>,
/// <c>screenReaderAssist</c>, <c>captions</c>.</para>
///
/// <para><see cref="TextSize"/> is the app's <b>stable enum name</b>, never an
/// index, and the client match is case-sensitive — so the four names below are
/// spelled exactly as the Dart <c>AppTextSize</c> cases are.</para>
/// </summary>
/// <param name="Configured">
/// Whether this account has ever SAVED a choice. Without it the GET cannot say
/// whether <c>normal / captions-on</c> is the user's actual pick or just the
/// shape of an empty row, and the two must not be confused: the app applies the
/// fetched values over its local cache at sign-in, so returning defaults for a
/// never-saved account would RESET a low-vision user who had already chosen
/// extraLarge + high contrast on the device. False means "nothing stored" and
/// the app keeps (and uploads) what it has.
/// </param>
public sealed record AccountPreferences(
    string TextSize,
    bool HighContrast,
    bool ReduceMotion,
    bool ScreenReaderAssist,
    bool Captions,
    bool Configured)
{
    /// <summary>صغير — the smallest text scale.</summary>
    public const string TextSizeSmall = "small";

    /// <summary>متوسط — the shipped default text scale.</summary>
    public const string TextSizeNormal = "normal";

    /// <summary>كبير — the enlarged text scale.</summary>
    public const string TextSizeLarge = "large";

    /// <summary>أكبر — the largest text scale.</summary>
    public const string TextSizeExtraLarge = "extraLarge";

    /// <summary>The text size a caller gets when it has never saved a choice,
    /// and the fallback for a stored value the client could not decode.</summary>
    public const string DefaultTextSize = TextSizeNormal;

    /// <summary>The four accepted <see cref="TextSize"/> names. A PUT carrying
    /// anything else is rejected rather than silently coerced, so a typo can
    /// never reach the column and read back as a different choice.</summary>
    public static readonly IReadOnlyList<string> AllowedTextSizes =
    [
        TextSizeSmall,
        TextSizeNormal,
        TextSizeLarge,
        TextSizeExtraLarge,
    ];

    /// <summary>What an account that has never saved a choice reads back —
    /// captions ON, everything else off, text size <c>normal</c>, and
    /// <see cref="Configured"/> FALSE. The GET returns this rather than a 404,
    /// and the false flag is what stops the app treating it as the user's pick.
    /// </summary>
    public static AccountPreferences Defaults { get; } =
        new(DefaultTextSize, false, false, false, true, Configured: false);

    /// <summary>True when <paramref name="textSize"/> is one of
    /// <see cref="AllowedTextSizes"/>. Ordinal (case-sensitive) on purpose: the
    /// app compares the name byte for byte, so <c>"ExtraLarge"</c> would decode
    /// as the fallback rather than as the user's pick.</summary>
    public static bool IsAllowedTextSize(string? textSize) =>
        textSize is not null
        && AllowedTextSizes.Contains(textSize, StringComparer.Ordinal);
}

/// <summary>
/// The body of <c>PUT /api/v1/app/account/preferences</c> — a full replace, so
/// the call is idempotent: sending the same body twice leaves the same row.
/// Every field is optional and falls back to its shipped default (captions ON,
/// the rest off), matching how the app decodes the response, so a partial body
/// from an older build is stored as a complete, well-defined set rather than
/// rejected.
/// </summary>
public sealed class UpdateAccountPreferencesRequest
{
    /// <summary>One of <see cref="AccountPreferences.AllowedTextSizes"/>. Null /
    /// absent means <see cref="AccountPreferences.DefaultTextSize"/>; any other
    /// value is a 400 <c>VALIDATION_FAILED</c>.</summary>
    public string? TextSize { get; set; }

    /// <summary>تباين عالٍ — high-contrast rendering. Defaults off.</summary>
    public bool HighContrast { get; set; }

    /// <summary>تقليل الحركة — suppress non-essential animation. Defaults off.</summary>
    public bool ReduceMotion { get; set; }

    /// <summary>قارئ الشاشة — announce each screen on navigation. Defaults off.</summary>
    public bool ScreenReaderAssist { get; set; }

    /// <summary>الترجمة النصية — show the live / session caption strip. The one
    /// choice that defaults <b>on</b>.</summary>
    public bool Captions { get; set; } = true;
}
