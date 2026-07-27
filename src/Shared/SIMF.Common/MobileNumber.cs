// Tests: SIMF.Api.Tests/MobileNumberTests.cs
using System.Text.RegularExpressions;

namespace SIMF.Common;

/// <summary>
/// C4 (D-371) — the ONE canonical form of a stored mobile number.
///
/// <para>DEF-PHN-003 — the shape rules always stripped separators before
/// matching, but only for the match: the value itself was persisted exactly as
/// typed, so the same column held <c>+966501234567</c> (the app, which
/// canonicalises client-side) and <c>+966-555987654</c> (the Control Panel /
/// Website phone input, which emits <c>+dial-local</c>). Two spellings of one
/// number defeat search, export and de-duplication. Every write path stores
/// <see cref="NormalizeOptional"/>'s output, so the column holds one form.</para>
///
/// <para><b>Two jobs, deliberately kept apart.</b> Stripping separators is not
/// enough on its own: D-371 accepts a Saudi mobile in TWO spellings —
/// <c>05XXXXXXXX</c> and <c>+9665XXXXXXXX</c> — and neither rewrites into the
/// other, so the same number still reached the column two ways and equality
/// checks still failed. <see cref="Canonicalize"/> converges them; the E.164
/// <c>+9665…</c> spelling is the one kept, because it is already one of the two
/// spellings D-371 accepts and it matches the international column's own format
/// — no third form is invented.</para>
///
/// <para><b>What is ACCEPTED does not change — only what is STORED.</b>
/// <see cref="Normalize"/> stays exactly the match form the D-371 shape rules are
/// applied to, and the validators keep matching against it. Folding the Saudi
/// local form inside <see cref="Normalize"/> would make <c>0501234567</c> satisfy
/// the E.164 test as well, so a Saudi local number would start being accepted
/// into the INTERNATIONAL field — a widening of the API's contract. The fold
/// therefore lives on the storage path alone, which validation has already run
/// before.</para>
///
/// <para>The client mirrors the accept rules in <c>phone_validation.dart</c>
/// (the same two Saudi shapes, the same E.164 shape, the same separator and
/// <c>00</c> handling), so both spellings the app may submit are still accepted
/// here and simply land in the column as the one canonical form.</para>
/// </summary>
public static class MobileNumber
{
    /// <summary>C4 (D-371) — the Saudi LOCAL spelling: a leading trunk <c>0</c>,
    /// then <c>5</c> and 8 more digits. Captured without the trunk zero, which is
    /// exactly what follows the country code in the international spelling.</summary>
    private static readonly Regex SaudiLocalShape =
        new(@"^0(5\d{8})$", RegexOptions.Compiled);

    /// <summary>The Saudi country code the local trunk <c>0</c> is exchanged for.</summary>
    private const string SaudiCountryCode = "+966";

    /// <summary>The MATCH form of <paramref name="value"/>: trimmed, spaces and
    /// dashes stripped, a leading <c>00</c> rewritten to <c>+</c>. So
    /// "+9665 0123-4567", "+966501234567" and "009665..." all reduce to the same
    /// string. This is what the D-371 shape rules are matched against, so it must
    /// keep a local number local — see the class remarks.</summary>
    public static string Normalize(string value)
    {
        var stripped = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return stripped.StartsWith("00", StringComparison.Ordinal)
            ? string.Concat("+", stripped.AsSpan(2))
            : stripped;
    }

    /// <summary>DEF-PHN-003 — the STORAGE form: <see cref="Normalize"/>, plus the
    /// Saudi local spelling folded onto the E.164 one, so both spellings D-371
    /// accepts persist as a single string. Only an exact <c>05</c> + 8 digits is
    /// folded; anything else — a non-Saudi international number, or a <c>05…</c>
    /// of the wrong length — is returned as <see cref="Normalize"/> left it, so no
    /// value is handed a country code it did not earn.</summary>
    public static string Canonicalize(string value)
    {
        var normalized = Normalize(value);
        var saudiLocal = SaudiLocalShape.Match(normalized);
        return saudiLocal.Success
            ? string.Concat(SaudiCountryCode, saudiLocal.Groups[1].Value)
            : normalized;
    }

    /// <summary>The canonical form for storage, or <c>null</c> when the value is
    /// blank — the shape every persistence path uses, so an absent number is a
    /// NULL column rather than an empty string and a present one is
    /// <see cref="Canonicalize"/>d.</summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Canonicalize(value);
}
