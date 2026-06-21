using System.Globalization;
using SIMF.Application.IdentityAccess;
using Xunit;

namespace SIMF.Application.Tests;

public class AccountCodeHasherTests
{
    [Fact]
    public void Hash_is_deterministic_and_bruteforce_recovers_the_plaintext()
    {
        AccountCodeHasher.ConfigureKey("0123456789abcdef0123456789abcdef");
        const string code = "012345";
        var stored = AccountCodeHasher.Hash(code);

        Assert.Equal(16, stored.Length);
        Assert.Equal(stored, AccountCodeHasher.Hash(code)); // deterministic

        string? found = null;
        for (var i = 0; i < 1_000_000; i++)
        {
            var candidate = i.ToString("D6", CultureInfo.InvariantCulture);
            if (AccountCodeHasher.Hash(candidate) == stored)
            {
                found = candidate;
                break;
            }
        }
        Assert.Equal(code, found);
    }
}
