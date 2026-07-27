using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// DEF-PHN-003 — unit tests for the shared <see cref="MobileNumber"/> helper.
///
/// <para>Scope settled at integration 2026-07-27: DEF-PHN-003 as filed is a
/// SEPARATOR defect (<c>+966501234567</c> vs <c>+966-555987654</c> — one spelling,
/// one dash apart), and that is what is fixed. The two spellings D-371 accepts
/// (<c>05XXXXXXXX</c> and <c>+9665XXXXXXXX</c>) are deliberately NOT folded onto
/// each other: two concurrent fixes chose OPPOSITE target forms, and picking one
/// rewrites stored data and changes duplicate detection, so it is an owner
/// decision. These tests pin the current, agreed contract.</para>
///
/// <para>Acceptance is deliberately NOT changed: <see cref="MobileNumber.Normalize"/>
/// stays the MATCH form the D-371 shapes are applied to, so a Saudi local number
/// is still not an E.164 international one. Only what is STORED changed.</para>
/// </summary>
public sealed class MobileNumberTests
{
    private const string SaudiCanonical = "0501234567";

    // Separator canonicalisation — the actual DEF-PHN-003 defect. However the user
    // typed the spacing, dashes or a leading 00, one spelling reaches the column as
    // one string.
    [Theory]
    [InlineData("0501234567", "0501234567")]
    [InlineData("05 0123-4567", "0501234567")]
    [InlineData("  050 123 4567  ", "0501234567")]
    [InlineData("+966501234567", "+966501234567")]
    [InlineData("+966-50-123-4567", "+966501234567")]
    [InlineData("  +966 50 123 4567  ", "+966501234567")]
    [InlineData("00966501234567", "+966501234567")]
    public void Separators_are_canonicalised_within_a_spelling(string typed, string stored)
        => Assert.Equal(stored, MobileNumber.NormalizeOptional(typed));

    // OPEN QUESTION, pinned so a future change is deliberate: the two accepted Saudi
    // spellings still store DIFFERENTLY. That is a known, owner-facing decision (see
    // the class remarks) — not an oversight. If someone folds them, this test fails
    // and they must make the choice consciously and record it.
    [Fact]
    public void The_two_accepted_saudi_spellings_are_not_folded_onto_each_other()
        => Assert.NotEqual(
            MobileNumber.NormalizeOptional("+966501234567"),
            MobileNumber.NormalizeOptional("0501234567"));

    // A non-Saudi international number keeps its own country code — the fold only
    // strips +966 from a Saudi MOBILE, it never de-internationalises anything else.
    [Theory]
    [InlineData("+441234567890", "+441234567890")]
    [InlineData("+44 1234-567890", "+441234567890")]
    [InlineData("00441234567890", "+441234567890")]
    [InlineData("+201234567890", "+201234567890")]
    public void A_non_saudi_international_number_is_unaffected(string typed, string stored)
        => Assert.Equal(stored, MobileNumber.NormalizeOptional(typed));

    // Only the EXACT Saudi shapes fold. A 05-prefixed value of any other length is
    // not a Saudi mobile, so it is left exactly as Normalize produced it.
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
