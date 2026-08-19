// Tests: SIMF.Api.Tests/ArabicCollationTests.cs - drives the fold end to end
//        through a real grid search, in both directions, plus the negative case
//        that stops a fold which matched everything from passing.
using System.Linq.Expressions;
using System.Reflection;

namespace SIMF.Common.Grids;

/// <summary>
/// Folds the Arabic letter forms that a collation cannot, so a grid search finds
/// the row whichever way the operator spelled the name.
///
/// <para>The <c>Arabic_CI_AI</c> collation on every <c>*Arabic</c> column folds the
/// alef maksura onto the yeh, and that is the whole of what it does for us.
/// Accent-insensitivity discards a SECONDARY weight, and the letters people
/// actually vary carry PRIMARY weights of their own: a precomposed
/// alef-with-hamza is simply a different letter from a bare alef to SQL Server,
/// and a teh marbuta is a different letter from a heh. Measured against the
/// engine rather than inferred - a bare-alef needle found none of the four
/// hamza-carrying alef forms until these replacements were applied, and found all
/// four afterwards.</para>
///
/// <para>The fold is a chain of <c>REPLACE</c> calls, applied to the COLUMN in SQL
/// and to the NEEDLE in memory. Both sides must fold identically or the search
/// silently misses forever, which is why the table below is the only definition of
/// it and both paths read from this one place. It costs nothing in index terms:
/// a substring search is a scan either way.</para>
///
/// <para>This is deliberately NOT a normalisation of stored data. The rows keep
/// exactly what was typed - a name is displayed the way its owner writes it - and
/// only the comparison is widened.</para>
///
/// <para><b>The teh marbuta is deliberately absent, and must stay absent.</b>
/// Folding it onto the heh is the other split Arabic typists produce, and it
/// cannot be done this way: SQL Server applies the COLUMN's collation to
/// <c>REPLACE</c>'s own needle, so on an accent-insensitive column a teh marbuta
/// needle also matches a plain teh - and the replacement silently rewrites every
/// teh in the value. Measured: adding it turned a word containing a teh into a
/// non-match against its own spelling, and rewrote the alphabet in a control
/// string. Every replacement below was checked the same way and leaves all other
/// letters untouched. Closing the teh split needs the fold performed under a
/// binary collation, which cannot be expressed here because this project
/// deliberately does not reference EF Core.</para>
/// </summary>
internal static class GridArabicFold
{
    /// <summary>Every replacement, in the order both sides apply them.
    ///
    /// <para>The combining marks are removed rather than mapped, because a
    /// DECOMPOSED alef-with-hamza is the bare alef followed by the mark: stripping
    /// the mark leaves the letter already correct, while mapping it would insert a
    /// second character and break the substring position. The precomposed forms are
    /// mapped instead, since there is nothing to strip.</para></summary>
    private static readonly (string From, string To)[] Replacements =
    [
        ("أ", "ا"),   // ALEF WITH HAMZA ABOVE   -> alef
        ("إ", "ا"),   // ALEF WITH HAMZA BELOW   -> alef
        ("آ", "ا"),   // ALEF WITH MADDA ABOVE   -> alef
        ("ٱ", "ا"),   // ALEF WASLA              -> alef
        ("ٔ", ""),         // COMBINING HAMZA ABOVE   -> removed
        ("ٕ", ""),         // COMBINING HAMZA BELOW   -> removed
    ];

    private static readonly MethodInfo StringReplace =
        typeof(string).GetMethod(nameof(string.Replace), [typeof(string), typeof(string)])!;

    /// <summary>True when this selector reads a column the fold applies to.
    ///
    /// <para>Keyed on the <c>*Arabic</c> property-name convention, the same one the
    /// collation is applied by, so a bilingual column added later is folded without
    /// anyone remembering to ask. A selector that is not a direct member access -
    /// the two grids that declare a key by concatenating a code or a title with its
    /// Arabic twin - is NOT folded: there is no single column to name, and widening
    /// a concatenation would fold the Latin half too. Those two are
    /// hamza-sensitive by design.</para></summary>
    internal static bool Applies(Expression body) =>
        body is MemberExpression { Member.Name: { } name }
        && name.EndsWith("Arabic", StringComparison.Ordinal);

    /// <summary>The needle, folded in memory. Must stay identical to
    /// <see cref="FoldColumn"/>; both read <see cref="Replacements"/>.</summary>
    internal static string Fold(string term)
    {
        var folded = term;
        foreach (var (from, to) in Replacements)
        {
            folded = folded.Replace(from, to, StringComparison.Ordinal);
        }
        return folded;
    }

    /// <summary>The column, folded in SQL: nested <c>REPLACE</c> calls that EF
    /// translates natively.</summary>
    internal static Expression FoldColumn(Expression body)
    {
        var folded = body;
        foreach (var (from, to) in Replacements)
        {
            folded = Expression.Call(
                folded, StringReplace,
                Expression.Constant(from, typeof(string)),
                Expression.Constant(to, typeof(string)));
        }
        return folded;
    }
}
