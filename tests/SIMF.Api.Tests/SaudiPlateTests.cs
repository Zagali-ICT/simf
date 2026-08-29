using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// C6 (D-459) — unit tests for the shared <see cref="SaudiPlate"/> helper: the
/// 17-letter validation, canonical Latin normalisation and the Arabic / English
/// renderings (mirrored by the Flutter <c>plate_validation_test.dart</c>).
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Profiles)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Fast)]
public sealed class SaudiPlateTests
{
    // Relaxed rule (owner 2026-07-06): at least one plate letter and/or at
    // least one digit — up to 3 letters + up to 4 digits, either order.
    [Theory]
    [InlineData("ABJ1234")]
    [InlineData("abj 1234")]
    [InlineData("1234-ABJ")]
    [InlineData("ابح1234")]   // Arabic plate letters
    [InlineData("ابح١٢٣٤")]   // + Arabic-Indic digits
    [InlineData("ABJ1")]
    [InlineData("AB1234")]    // 2 letters + digits
    [InlineData("A1")]        // the floor: one letter + one digit
    public void IsValid_accepts_a_plate_carrying_letters_AND_digits(string value)
        => Assert.True(SaudiPlate.IsValid(value));

    // Owner 2026-08-29 tightened this: a plate that IS entered must carry both.
    // Until then {0,n} quantifiers accepted either half alone.
    [Theory]
    [InlineData("ABJ")]       // letters only - accepted before 2026-08-29
    [InlineData("1234")]      // digits only  - accepted before 2026-08-29
    public void IsValid_rejects_a_plate_missing_letters_or_digits(string value)
        => Assert.False(SaudiPlate.IsValid(value));

    [Theory]
    [InlineData("ABCD123")]   // 4 letters
    [InlineData("ABJ12345")]  // 5 digits
    [InlineData("12345")]     // 5 digits
    [InlineData("AB!1234")]   // symbol
    [InlineData("ABC1234")]   // C is not one of the 17 plate letters
    [InlineData("ابج1234")]   // ج (jeem) is not a plate letter
    [InlineData("")]
    [InlineData(null)]
    public void IsValid_rejects_out_of_range_or_non_plate_chars(string? value)
        => Assert.False(SaudiPlate.IsValid(value));

    [Theory]
    [InlineData("abj 1234", "ABJ1234")]
    [InlineData("ابح1234", "ABJ1234")]
    [InlineData("ابح١٢٣٤", "ABJ1234")]
    [InlineData("1234-ABJ", "1234ABJ")]
    public void Normalize_produces_the_canonical_latin_code(string value, string expected)
        => Assert.Equal(expected, SaudiPlate.Normalize(value));

    [Fact]
    public void Normalize_returns_null_for_blank()
        => Assert.Null(SaudiPlate.Normalize("  "));

    [Theory]
    [InlineData("ABJ1234", "ابح١٢٣٤")]
    [InlineData("ابح1234", "ابح١٢٣٤")]
    public void ToArabic_renders_arabic_letters_and_digits(string value, string expected)
        => Assert.Equal(expected, SaudiPlate.ToArabic(value));

    [Fact]
    public void Letters_list_holds_the_17_official_plate_letters()
        => Assert.Equal(17, SaudiPlate.Letters.Count);

    [Fact]
    public void Round_trips_losslessly_between_scripts()
    {
        const string code = "ABJ1234";
        var arabic = SaudiPlate.ToArabic(code);
        Assert.Equal(code, SaudiPlate.ToEnglish(arabic));
    }
}
