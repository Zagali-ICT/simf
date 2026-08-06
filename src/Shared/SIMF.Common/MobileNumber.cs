// Tests: SIMF.Api.Tests/MobileNumberTests.cs

namespace SIMF.Common;

/// <summary>
/// The ONE canonical form of a stored mobile number.
///
/// <para>DEF-PHN-003 — the shape rules always stripped separators before
/// matching, but only for the match: the value itself was persisted exactly as
/// typed, so the same column held <c>+966501234567</c> (the app, which
/// canonicalises client-side) and <c>+966-555987654</c> (the Control Panel /
/// Website phone input, which emits <c>+dial-local</c>). Two spellings of one
/// number defeat search, export and de-duplication. Every write path stores
/// <see cref="NormalizeOptional"/>'s output, so the column holds one form.</para>
///
/// <para><b>Scope, settled at integration 2026-07-27.</b> DEF-PHN-003 as filed is
/// a SEPARATOR defect: the column held <c>+966501234567</c> and
/// <c>+966-555987654</c> — the same spelling, differing only by a dash.
/// <see cref="NormalizeOptional"/> closes exactly that.
///
/// It does NOT fold the two spellings D-371 accepts (<c>05XXXXXXXX</c> and
/// <c>+9665XXXXXXXX</c>) onto each other. A concurrent fix tried to, and the two
/// branches picked OPPOSITE target forms — the disagreement only surfaced when they
/// were merged, with each side's tests asserting the other's output was wrong.
/// Picking a canonical spelling rewrites stored values and changes duplicate
/// detection, search and export, so it is an OWNER decision, not a merge decision.
/// It is recorded as an open item rather than silently chosen here. Both spellings
/// are still ACCEPTED and both still round-trip unchanged apart from separators.</para>
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

    /// <summary>DEF-PHN-003 — the STORAGE form. Currently identical to
    /// <see cref="Normalize"/>: separators stripped and <c>00</c> rewritten to
    /// <c>+</c>, which is the whole of the defect as filed. Kept as its own name so
    /// the storage path has a single seam to change if the owner later decides to
    /// fold the two accepted Saudi spellings onto one (see the class remarks).</summary>
    public static string Canonicalize(string value) => Normalize(value);

    /// <summary>The canonical form for storage, or <c>null</c> when the value is
    /// blank — the shape every persistence path uses, so an absent number is a
    /// NULL column rather than an empty string and a present one is
    /// <see cref="Canonicalize"/>d.</summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Canonicalize(value);
}
