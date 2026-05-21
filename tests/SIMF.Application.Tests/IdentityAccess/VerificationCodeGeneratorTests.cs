using SIMF.Application.IdentityAccess;
using Xunit;

namespace SIMF.Application.Tests.IdentityAccess;

/// <summary>Unit tests for <see cref="VerificationCodeGenerator"/>.</summary>
public sealed class VerificationCodeGeneratorTests
{
    [Fact]
    public void Generate_always_returns_six_digits()
    {
        for (var i = 0; i < 1_000; i++)
        {
            Assert.Matches(@"^\d{6}$", VerificationCodeGenerator.Generate());
        }
    }

    [Fact]
    public void Generate_produces_many_distinct_values()
    {
        var codes = new HashSet<string>();
        for (var i = 0; i < 500; i++)
        {
            codes.Add(VerificationCodeGenerator.Generate());
        }

        // 500 draws from a 1,000,000 space — near-zero collision probability.
        Assert.True(codes.Count > 490);
    }
}
