namespace SIMF.Common;

/// <summary>
/// C4 (D-371) — the ONE canonical form of a stored mobile number.
///
/// <para>DEF-PHN-003 — the shape rules always stripped separators before
/// matching, but only for the match: the value itself was persisted exactly as
/// typed, so the same column held <c>+966501234567</c> (the app, which
/// canonicalises client-side) and <c>+966-555987654</c> (the Control Panel /
/// Website phone input, which emits <c>+dial-local</c>). Two spellings of one
/// number defeat search, export and de-duplication. Every write path now stores
/// <see cref="Normalize"/>'s output, so the column holds one form.</para>
///
/// <para>The rule is the one the validators already used and the client mirrors
/// (<c>phone_validation.dart</c>): spaces and dashes are removed, and a leading
/// international <c>00</c> prefix becomes <c>+</c>. Nothing else is rewritten —
/// this canonicalises, it does not widen or narrow what validates.</para>
/// </summary>
public static class MobileNumber
{
    /// <summary>The canonical form of <paramref name="value"/>: trimmed, spaces
    /// and dashes stripped, a leading <c>00</c> rewritten to <c>+</c>. So
    /// "+9665 0123-4567", "+966501234567" and "009665..." all reduce to the same
    /// string.</summary>
    public static string Normalize(string value)
    {
        var stripped = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return stripped.StartsWith("00", StringComparison.Ordinal)
            ? string.Concat("+", stripped.AsSpan(2))
            : stripped;
    }

    /// <summary>The canonical form for storage, or <c>null</c> when the value is
    /// blank — the shape every persistence path uses so an absent number is a
    /// NULL column, never an empty string.</summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Normalize(value);
}
