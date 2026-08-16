// Tests: SIMF.Api.Tests/MobileNumberTests.cs

namespace SIMF.Common;

/// <summary>
/// The ONE canonical form of a stored mobile number.
///
/// <para>The shape rules always stripped separators before
/// matching, but only for the match: the value itself was persisted exactly as
/// typed, so the same column held <c>+966501234567</c> (the app, which
/// canonicalises client-side) and <c>+966-555987654</c> (the Control Panel /
/// Website phone input, which emits <c>+dial-local</c>). Two spellings of one
/// number defeat search, export and de-duplication. Every write path stores
/// <see cref="NormalizeOptional"/>'s output, so the column holds one form.</para>
///
/// <para><b>Scope.</b> It began as a SEPARATOR fix: the column held
/// <c>+966501234567</c> and <c>+966-555987654</c> — the same spelling, differing
/// only by a dash.
///
/// It now ALSO folds the two accepted Saudi spellings (<c>05XXXXXXXX</c> and
/// <c>+9665XXXXXXXX</c>) onto one, which an earlier round left open as an owner
/// decision because two concurrent fixes had picked OPPOSITE target forms. The
/// mobile-number collapse settles it, and settles the direction with it:
/// <b><c>+966</c> wins</b>. A Saudi mobile IS an international mobile with a
/// country code, so <c>+966…</c> is the only form that one column can hold for a
/// Saudi and a non-Saudi number alike; folding the other way would need the
/// column to know which country it was looking at. Both spellings are still
/// ACCEPTED, and both now STORE as <c>+9665XXXXXXXX</c>, so the same number
/// entered two ways de-duplicates against itself instead of against
/// nothing.</para>
///
/// <para><b>What is ACCEPTED does not change — only what is STORED.</b>
/// <see cref="Normalize"/> stays exactly the match form the shape rules are
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
    /// <summary>Saudi Arabia's country calling code, the prefix every canonical
    /// Saudi number carries.</summary>
    public const string SaudiDialPrefix = "+966";

    // The Saudi local mobile spelling, separators already stripped: 05 then eight
    // digits. Half of the SaudiMobileShape the validators match with — the other
    // half is the +9665XXXXXXXX form this folds onto.
    private static readonly System.Text.RegularExpressions.Regex SaudiLocalShape =
        new(@"^05\d{8}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Whether a CANONICAL number is Saudi. The one place that knows what
    /// makes a number Saudi, so the storage split cannot drift from the fold
    /// above.</summary>
    public static bool IsSaudi(string? canonical) =>
        canonical?.StartsWith(SaudiDialPrefix, StringComparison.Ordinal) == true;

    /// <summary>The MATCH form of <paramref name="value"/>: trimmed, spaces and
    /// dashes stripped, a leading <c>00</c> rewritten to <c>+</c>. So
    /// "+9665 0123-4567", "+966501234567" and "009665..." all reduce to the same
    /// string. This is what the shape rules are matched against, so it must
    /// keep a local number local — see the class remarks.</summary>
    public static string Normalize(string value)
    {
        var stripped = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return stripped.StartsWith("00", StringComparison.Ordinal)
            ? string.Concat("+", stripped.AsSpan(2))
            : stripped;
    }

    /// <summary>The STORAGE form: <see cref="Normalize"/>, then the Saudi local
    /// <c>05XXXXXXXX</c> spelling folded onto <c>+9665XXXXXXXX</c> so one number
    /// is one string in the column.
    ///
    /// <para>This is the seam the class remarks describe, and folding HERE rather
    /// than in <see cref="Normalize"/> is the whole point: the validators match
    /// against <see cref="Normalize"/>, so a Saudi local number still fails the
    /// E.164 test and still cannot be posted into the INTERNATIONAL field.
    /// Widening what is stored must not widen what is accepted.</para>
    ///
    /// <para>Only the EXACT Saudi mobile shape folds. A <c>05</c>-prefixed value
    /// of any other length is not a Saudi mobile, so it is left as
    /// <see cref="Normalize"/> produced it rather than being given a country code
    /// it has no claim to.</para></summary>
    public static string Canonicalize(string value)
    {
        var normalized = Normalize(value);
        return SaudiLocalShape.IsMatch(normalized)
            ? string.Concat(SaudiDialPrefix, normalized.AsSpan(1))
            : normalized;
    }

    /// <summary>The canonical form for storage, or <c>null</c> when the value is
    /// blank — the shape every persistence path uses, so an absent number is a
    /// NULL column rather than an empty string and a present one is
    /// <see cref="Canonicalize"/>d.</summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Canonicalize(value);
}
