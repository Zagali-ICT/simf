// Tests: SIMF.Application.Tests/ConstantTimeTests.cs (equal, unequal, differing
//        lengths, and null handling).
using System.Security.Cryptography;
using System.Text;

namespace SIMF.Application.Security;

/// <summary>
/// Constant-time comparison of two secrets.
///
/// <para>Five services each carried a verbatim private copy of this. The whole
/// purpose of the helper is that comparison time must not depend on how many
/// leading characters happen to match, which makes it exactly the kind of code
/// that should have one implementation to audit rather than five to keep in
/// step — a copy that drifted into <c>==</c> would still pass every functional
/// test while leaking the secret through timing.</para>
/// </summary>
public static class ConstantTime
{
    /// <summary>True when the two values are equal, taking the same time whether
    /// they differ at the first character or the last. Null is treated as empty,
    /// so a missing value compares unequal to any real secret rather than
    /// throwing at a security boundary.</summary>
    public static bool Matches(string? left, string? right) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(left ?? string.Empty),
            Encoding.UTF8.GetBytes(right ?? string.Empty));
}
