using SIMF.Common;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Guards the generated demo-account seed password against the product's own
/// password policy.
/// </summary>
/// <remarks>
/// The fixture generates this password per test process (DEF-SEC-001: it must
/// not be committed) and hands it to the seeder as <c>Seed:DemoPassword</c>. It
/// used to be built as <c>$"TestOnly!{Guid.NewGuid():N}Aa1"</c> on the
/// assumption that the SHAPE was enough - upper, lower, digit, symbol. It was
/// not: a 32-character hex GUID regularly contains a sequential run ("abc",
/// "123", "234") or three repeated characters, and <see cref="PasswordPolicy"/>
/// rejects both. On an unlucky draw every demo @simf.local account failed to
/// seed, and the visible damage landed far away - three IdentitySeederTests and
/// two DemoOperationalConfigSeederTests failing with "Value is null".
///
/// It read as suite interference for weeks because it reproduced only in full
/// runs and never when a class was re-run alone. That is not interference: a
/// re-run is a NEW PROCESS, and a new process draws a new GUID.
///
/// This test is cheap insurance against the whole class of bug returning: it
/// asserts the value the fixture actually publishes, using the product's rules
/// rather than a restatement of them.
/// </remarks>
public sealed class DemoSeedPasswordTests
{
    [Fact]
    public void The_generated_demo_password_satisfies_the_product_password_policy()
    {
        // Constructing the factory is what publishes Seed__DemoPassword; no host
        // is booted and no database is touched.
        using var factory = new SimfApiFactory();

        var password = Environment.GetEnvironmentVariable("Seed__DemoPassword");

        Assert.False(
            string.IsNullOrWhiteSpace(password),
            "The fixture must publish Seed:DemoPassword; the demo accounts cannot "
            + "seed without it.");

        Assert.False(
            PasswordPolicy.HasSequentialRun(password!),
            "The generated demo password contains a sequential run, so every demo "
            + "@simf.local account will silently fail to seed on this run and the "
            + "failures will surface far away as null users.");
        Assert.False(
            PasswordPolicy.HasRepeatRun(password!),
            "The generated demo password repeats a character three times, which the "
            + "password policy rejects, so the demo accounts will not seed.");
        Assert.False(
            PasswordPolicy.IsCommon(password!),
            "The generated demo password is on the common-password list.");

        Assert.True(PasswordPolicy.HasUppercase(password!));
        Assert.True(PasswordPolicy.HasLowercase(password!));
        Assert.True(PasswordPolicy.HasDigit(password!));
        Assert.True(PasswordPolicy.HasSpecial(password!));
    }
}
