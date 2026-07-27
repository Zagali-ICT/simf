using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// DEF-PHN-003 — unit tests for the shared <see cref="MobileNumber"/> helper.
///
/// <para>The C4 / D-371 rules accept a Saudi mobile in TWO spellings —
/// <c>05XXXXXXXX</c> and <c>+9665XXXXXXXX</c> — so stripping separators alone
/// still let one number reach the column two different ways, and equality checks
/// and duplicate detection went on failing. The stored form must be ONE of them
/// for both spellings; the E.164 <c>+9665…</c> form is the one kept, matching the
/// international column's own format (no third form is invented).</para>
///
/// <para>Acceptance is deliberately NOT changed: <see cref="MobileNumber.Normalize"/>
/// stays the MATCH form the D-371 shapes are applied to, so a Saudi local number
/// is still not an E.164 international one. Only what is STORED changed.</para>
/// </summary>
public sealed class MobileNumberTests
{
    private const string SaudiCanonical = "+966501234567";

    // Both accepted spellings — however the user typed the separators — reach the
    // column as the same string.
    [Theory]
    [InlineData("0501234567")]
    [InlineData("+966501234567")]
    [InlineData("00966501234567")]
    [InlineData("05 0123-4567")]
    [InlineData("  +966 50 123 4567  ")]
    public void Every_accepted_saudi_spelling_stores_as_one_form(string typed)
        => Assert.Equal(SaudiCanonical, MobileNumber.NormalizeOptional(typed));

    // The DEF-PHN-003 defect, stated directly: the two accepted spellings of the
    // SAME number must not produce two different stored values.
    [Fact]
    public void The_two_accepted_saudi_spellings_store_identically()
        => Assert.Equal(
            MobileNumber.NormalizeOptional("+966501234567"),
            MobileNumber.NormalizeOptional("0501234567"));

    // A non-Saudi international number keeps its own country code — the fold is
    // the Saudi plan's, not a blanket "prefix everything with +966".
    [Theory]
    [InlineData("+441234567890", "+441234567890")]
    [InlineData("+44 1234-567890", "+441234567890")]
    [InlineData("00441234567890", "+441234567890")]
    [InlineData("+201234567890", "+201234567890")]
    public void A_non_saudi_international_number_is_unaffected(string typed, string stored)
        => Assert.Equal(stored, MobileNumber.NormalizeOptional(typed));

    // Only the EXACT Saudi local shape (05 + 8 digits) folds. A 05-prefixed value
    // of any other length is not a Saudi mobile, so it is never handed a +966 it
    // did not earn.
    [Theory]
    [InlineData("05012345")]
    [InlineData("050123456789")]
    [InlineData("0512345")]
    public void A_value_that_is_not_the_saudi_local_shape_is_left_alone(string typed)
        => Assert.Equal(typed, MobileNumber.NormalizeOptional(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_number_stores_as_null(string? typed)
        => Assert.Null(MobileNumber.NormalizeOptional(typed));

    // ACCEPTANCE GUARD — Normalize is what the D-371 shape rules match against.
    // It must keep the local form local: folding it here would make a Saudi 05…
    // pass the E.164 test and be accepted into the INTERNATIONAL field, widening
    // what the API accepts. Canonicalisation belongs on the storage path only.
    [Theory]
    [InlineData("0501234567", "0501234567")]
    [InlineData("05 0123-4567", "0501234567")]
    [InlineData("+966501234567", "+966501234567")]
    [InlineData("00966501234567", "+966501234567")]
    public void Normalize_is_the_match_form_and_never_promotes_a_local_number(
        string typed, string matched)
        => Assert.Equal(matched, MobileNumber.Normalize(typed));
}
