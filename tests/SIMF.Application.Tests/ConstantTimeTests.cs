using SIMF.Application.Security;
using Xunit;

namespace SIMF.Application.Tests;

/// <summary>
/// These assert behaviour, not timing — a unit test cannot prove constant time on
/// a shared CI box. The value is that the five services which used to carry their
/// own copy now share one implementation whose edge cases are pinned, so a future
/// edit that "simplifies" it to == fails here rather than leaking silently.
/// </summary>
public class ConstantTimeTests
{
    [Fact]
    public void Identical_values_match() =>
        Assert.True(ConstantTime.Matches("a1b2c3d4", "a1b2c3d4"));

    [Theory]
    [InlineData("a1b2c3d4", "a1b2c3d5")]   // differs at the last character
    [InlineData("a1b2c3d4", "b1b2c3d4")]   // differs at the first
    [InlineData("a1b2c3d4", "a1b2c3d")]    // shorter
    [InlineData("a1b2c3d4", "a1b2c3d44")]  // longer
    [InlineData("", "a")]
    public void Different_values_do_not_match(string left, string right) =>
        Assert.False(ConstantTime.Matches(left, right));

    [Fact]
    public void Null_is_treated_as_empty_rather_than_throwing()
    {
        // A verification path reaching this with a null must get a clean "no match",
        // not an exception at a security boundary.
        Assert.False(ConstantTime.Matches(null, "a1b2c3d4"));
        Assert.False(ConstantTime.Matches("a1b2c3d4", null));
        Assert.True(ConstantTime.Matches(null, null));
        Assert.True(ConstantTime.Matches(null, string.Empty));
    }
}
