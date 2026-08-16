using System.Globalization;

namespace SIMF.Api.Tests;

/// <summary>
/// H-1 — mint unique, Luhn-valid identifiers per call. The API-test classes each
/// share one DB and the registration paths dedup on the identity blind index, so
/// every test's request must carry a distinct identity. Seeded from a random base
/// so parallel test classes (each on its own DB) never coincide.
/// </summary>
internal static class TestIdentity
{
    private static int _identitySeq = Random.Shared.Next(1, 80_000_000);

    public static string MintNationalId() => MintIdentity('1');

    public static string MintIqama() => MintIdentity('2');

    /// <summary>A unique passport number in the validator's shape (6-9 letters or
    /// digits). Leads with letters so it can never collide with a minted national
    /// id or Iqama, and carries no check digit — passports have no universal
    /// checksum, and the validator only length-checks them.</summary>
    public static string MintPassport()
    {
        var n = System.Threading.Interlocked.Increment(ref _identitySeq) % 900_000;
        return "PP" + n.ToString("D6", CultureInfo.InvariantCulture);
    }

    private static string MintIdentity(char prefix)
    {
        var n = System.Threading.Interlocked.Increment(ref _identitySeq) % 90_000_000;
        var body = prefix + n.ToString("D8", CultureInfo.InvariantCulture);
        return body + LuhnCheckDigit(body).ToString(CultureInfo.InvariantCulture);
    }

    // Computes the Luhn check digit that goes at the END of the number (position 0
    // from the right — not doubled), so the partial's rightmost digit IS doubled.
    public static int LuhnCheckDigit(string partialWithoutCheck)
    {
        var sum = 0;
        var doubleDigit = true;
        for (var i = partialWithoutCheck.Length - 1; i >= 0; i--)
        {
            var digit = partialWithoutCheck[i] - '0';
            if (doubleDigit)
            {
                digit *= 2;
                if (digit > 9) { digit -= 9; }
            }
            sum += digit;
            doubleDigit = !doubleDigit;
        }
        return (10 - (sum % 10)) % 10;
    }
}
