using SIMF.Application.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>Unit tests for <see cref="VerificationCodeGenerator"/>.</summary>
public sealed class VerificationCodeGeneratorTests
{
    [Fact]
    public void Generate_returns_a_six_digit_code()
    {
        var code = VerificationCodeGenerator.Generate();

        Assert.Matches(@"^\d{6}$", code);
    }
}
