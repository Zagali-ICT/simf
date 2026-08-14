using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SIMF.Application.IdentityAccess;
using SIMF.Application.MeetingRequests;
using Xunit;

namespace SIMF.Application.Tests;

public class AccountCodeHasherTests
{
    private const string MasterKey = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Hash_is_deterministic_and_bruteforce_recovers_the_plaintext()
    {
        AccountCodeHasher.ConfigureKey(MasterKey);
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

    [Fact]
    public void The_two_hashers_derive_separate_keys_from_one_master_secret()
    {
        // One configured value reaches both hashers and also signs bearer tokens.
        // The derivation labels are the only thing keeping their key material
        // apart, so this is the assertion that the separation is real rather than
        // just described in a comment.
        const string input = "012345";
        AccountCodeHasher.ConfigureKey(MasterKey);
        MeetingActionTokenHasher.ConfigureKey(MasterKey);

        var accountCode = AccountCodeHasher.Hash(input);
        var meetingAction = MeetingActionTokenHasher.Hash(input);

        Assert.NotEqual(accountCode, meetingAction[..accountCode.Length]);

        // Neither may be a bare HMAC under the master key itself. Without this the
        // test would still pass if the derivation were dropped tomorrow: raw key
        // reuse is equally deterministic and equally brute-forceable, so only
        // naming the rejected implementation catches a regression to it.
        var rawMasterHmac = Convert.ToHexStringLower(
            HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(MasterKey), Encoding.UTF8.GetBytes(input)));

        Assert.NotEqual(rawMasterHmac[..accountCode.Length], accountCode);
        Assert.NotEqual(rawMasterHmac, meetingAction);
    }
}
