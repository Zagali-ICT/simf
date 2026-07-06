using System.Text;
using System.Text.RegularExpressions;

namespace SIMF.Common;

/// <summary>
/// C6 (D-459) — the official Saudi vehicle-plate letter set: the 17 Arabic
/// letters permitted on KSA plates and their single-character Latin codes
/// (the Elm / Absher transliteration). A plate is up to 3 letters (all from
/// this set, one script) and up to 4 digits, in either order, with at least
/// one letter and/or one digit (relaxed 2026-07-06, was a fixed 3 + 1–4).
///
/// <para>The letter set is a fixed bijection, so a plate written in one
/// script is losslessly convertible to the other. The system stores the
/// canonical Latin <b>code</b> (Latin letters + Western digits) and surfaces
/// both the Arabic and the English rendering on read — no duplicated
/// persistence, no extra column (the existing <c>UserProfile.PlateNumber</c>
/// holds the code). The 17 entries in <see cref="Letters"/> are the list the
/// app renders in its plate-letter dropdowns.</para>
///
/// <para>Input tolerance: separators (space / dash) are stripped, the
/// alef-with-hamza variants (أ إ آ ٱ) fold to plain alef (ا), the dotted
/// yaa (ي) folds to the plate's alef-maksura (ى → V), Arabic-Indic digits
/// (٠–٩) fold to Western, and Latin letters upper-case — so what the user
/// types validates and canonicalises the same way the client mirror does
/// (<c>saudi_plate.dart</c>).</para>
/// </summary>
public static class SaudiPlate
{
    /// <summary>One permitted plate letter: its stable Latin <paramref name="Code"/>
    /// (== the English glyph), its Arabic glyph and its English glyph.</summary>
    public sealed record PlateLetter(string Code, string Arabic, string English);

    /// <summary>The 17 permitted letters, in the canonical Elm / Absher order.</summary>
    public static readonly IReadOnlyList<PlateLetter> Letters =
    [
        new("A", "ا", "A"),
        new("B", "ب", "B"),
        new("J", "ح", "J"),
        new("D", "د", "D"),
        new("R", "ر", "R"),
        new("S", "س", "S"),
        new("X", "ص", "X"),
        new("T", "ط", "T"),
        new("E", "ع", "E"),
        new("G", "ق", "G"),
        new("K", "ك", "K"),
        new("L", "ل", "L"),
        new("Z", "م", "Z"),
        new("N", "ن", "N"),
        new("H", "ه", "H"),
        new("U", "و", "U"),
        new("V", "ى", "V"),
    ];

    private static readonly Dictionary<char, char> ArabicToLatin =
        Letters.ToDictionary(letter => letter.Arabic[0], letter => letter.English[0]);

    private static readonly Dictionary<char, char> LatinToArabic =
        Letters.ToDictionary(letter => letter.English[0], letter => letter.Arabic[0]);

    // The 17 Latin codes and the 17 Arabic glyphs, as regex character classes.
    private const string LatinClass = "ABDEGHJKLNRSTUVXZ";
    private const string ArabicClass = "ابحدرسصطعقكلمنهوى";

    // Relaxed rule (owner 2026-07-06): at least one plate letter and/or at
    // least one digit — up to 3 letters (from the 17-letter set, one script)
    // and up to 4 digits, in either order. The (?=.) lookahead rejects an
    // all-empty match. Mirrors the client (plate_validation.dart).
    private static readonly Regex LatinPlate = new(
        $"^(?=.)([{LatinClass}]{{0,3}}[0-9]{{0,4}}|[0-9]{{0,4}}[{LatinClass}]{{0,3}})$",
        RegexOptions.Compiled);

    private static readonly Regex ArabicPlate = new(
        $"^(?=.)([{ArabicClass}]{{0,3}}[0-9]{{0,4}}|[0-9]{{0,4}}[{ArabicClass}]{{0,3}})$",
        RegexOptions.Compiled);

    /// <summary>Strips separators and folds the input to a canonical
    /// comparison form (variant Arabic glyphs normalised, Arabic-Indic
    /// digits → Western, Latin upper-cased). Character order is preserved.</summary>
    private static string Prepare(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var original in value.Trim())
        {
            var folded = original switch
            {
                ' ' or '-' or ' ' => '\0',
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ي' => 'ى',
                >= '٠' and <= '٩' => (char)('0' + (original - '٠')),
                >= 'a' and <= 'z' => char.ToUpperInvariant(original),
                _ => original,
            };
            if (folded != '\0') { builder.Append(folded); }
        }
        return builder.ToString();
    }

    /// <summary>True when <paramref name="value"/> is a valid Saudi plate —
    /// at least one letter from the 17-letter set (one script) and/or at least
    /// one digit: up to 3 letters + up to 4 digits, either order (separators
    /// ignored; owner 2026-07-06). Empty / null is not valid; the caller owns
    /// the optional-field rule.</summary>
    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return false; }
        var prepared = Prepare(value);
        return LatinPlate.IsMatch(prepared) || ArabicPlate.IsMatch(prepared);
    }

    /// <summary>The canonical Latin <b>code</b> (Latin letters + Western
    /// digits, separators stripped) for storage, or null when blank. Assumes
    /// the value already passed <see cref="IsValid"/>; any unmapped character
    /// is left as-is.</summary>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        var prepared = Prepare(value);
        var builder = new StringBuilder(prepared.Length);
        foreach (var character in prepared)
        {
            builder.Append(ArabicToLatin.TryGetValue(character, out var latin) ? latin : character);
        }
        return builder.ToString();
    }

    /// <summary>The Arabic rendering (Arabic letters + Arabic-Indic digits)
    /// of any plate input, or null when blank.</summary>
    public static string? ToArabic(string? value)
    {
        var code = Normalize(value);
        if (code is null) { return null; }
        var builder = new StringBuilder(code.Length);
        foreach (var character in code)
        {
            if (LatinToArabic.TryGetValue(character, out var arabic))
            {
                builder.Append(arabic);
            }
            else if (character is >= '0' and <= '9')
            {
                builder.Append((char)('٠' + (character - '0')));
            }
            else
            {
                builder.Append(character);
            }
        }
        return builder.ToString();
    }

    /// <summary>The English rendering (Latin letters + Western digits) — the
    /// canonical code — of any plate input, or null when blank.</summary>
    public static string? ToEnglish(string? value) => Normalize(value);
}
