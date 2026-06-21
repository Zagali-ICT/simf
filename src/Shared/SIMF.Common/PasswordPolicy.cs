using System.Text;

namespace SIMF.Common;

/// <summary>
/// A7-10 / A7-28 / A7-29 (NCA Secure Application-Development Standard) — the single
/// source of truth for the SIMF password policy's structural checks. Both the
/// request-time FluentValidation rule (<c>PasswordRules.StrongPassword</c>) and the
/// central ASP.NET-Identity password validator delegate here so every credential
/// path enforces the same rules with one definition.
///
/// <para>The NCA standard requires (A7-29): at least one upper-case, one lower-case,
/// one digit and one special character; and rejects more than two identical
/// consecutive characters (e.g. "aaa"/"111"), sequential runs (e.g. "abc"/"123"),
/// the user's own identifier, and common dictionary passwords (incl. leet-speak
/// spellings such as "p@ssw0rd"). Length stays at the SIMF-API-001 §12.5 baseline
/// (8) and a passphrase ceiling of 128 (A7-10 — long passphrases are allowed).</para>
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;
    public const int MaxLength = 128;

    public static bool HasUppercase(string password) => password.Any(char.IsUpper);

    public static bool HasLowercase(string password) => password.Any(char.IsLower);

    public static bool HasDigit(string password) => password.Any(char.IsDigit);

    /// <summary>A special character is anything that is not a letter or a digit.</summary>
    public static bool HasSpecial(string password) =>
        password.Any(ch => !char.IsLetterOrDigit(ch));

    /// <summary>True when three or more identical characters appear in a row
    /// (e.g. "aaa", "111"). Two in a row is allowed.</summary>
    public static bool HasRepeatRun(string password)
    {
        for (var i = 0; i + 2 < password.Length; i++)
        {
            if (password[i] == password[i + 1] && password[i + 1] == password[i + 2])
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>True when three or more sequential characters appear in a row,
    /// ascending or descending, within the same class (letters or digits) —
    /// e.g. "abc", "123", "cba", "987".</summary>
    public static bool HasSequentialRun(string password)
    {
        for (var i = 0; i + 2 < password.Length; i++)
        {
            var a = char.ToLowerInvariant(password[i]);
            var b = char.ToLowerInvariant(password[i + 1]);
            var c = char.ToLowerInvariant(password[i + 2]);

            var allLetters = char.IsLetter(a) && char.IsLetter(b) && char.IsLetter(c);
            var allDigits = char.IsDigit(a) && char.IsDigit(b) && char.IsDigit(c);
            if (!allLetters && !allDigits)
            {
                continue;
            }
            if ((b - a == 1 && c - b == 1) || (a - b == 1 && b - c == 1))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>True when the password equals the email address or its local part
    /// (case-insensitive) — A7-29 "must not be the same as the username".</summary>
    public static bool ResemblesIdentifier(string password, string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }
        if (string.Equals(password, email, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        var at = email.IndexOf('@');
        var local = at > 0 ? email[..at] : email;
        return local.Length >= 3
            && string.Equals(password, local, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the password is a well-known weak/common password —
    /// either an exact match of a common password, or (after folding leet-speak
    /// substitutions and stripping non-letters) a common dictionary base word.
    /// So "password", "Passw0rd", "p@ssw0rd" and "PASSWORD" are all rejected.</summary>
    public static bool IsCommon(string password)
    {
        if (RawCommon.Contains(password))
        {
            return true;
        }
        var normalized = Normalize(password);
        return normalized.Length >= 3 && BaseWords.Contains(normalized);
    }

    /// <summary>Lower-cases, folds common leet-speak digits/symbols back to their
    /// letters, and drops every remaining non-letter so a dictionary base word
    /// can be matched regardless of the user's "clever" substitutions.</summary>
    private static string Normalize(string password)
    {
        var builder = new StringBuilder(password.Length);
        foreach (var ch in password.ToLowerInvariant())
        {
            if (Leet.TryGetValue(ch, out var mapped))
            {
                builder.Append(mapped);
            }
            else if (char.IsLetter(ch))
            {
                builder.Append(ch);
            }
            // Anything else (a digit with no letter mapping, punctuation) is dropped.
        }
        return builder.ToString();
    }

    private static readonly Dictionary<char, char> Leet = new()
    {
        ['0'] = 'o', ['1'] = 'i', ['3'] = 'e', ['4'] = 'a', ['5'] = 's',
        ['7'] = 't', ['8'] = 'b', ['9'] = 'g', ['@'] = 'a', ['$'] = 's',
    };

    /// <summary>Exact-match list of the most common raw passwords (numeric and
    /// mixed forms the leet-folding can't reduce to a single base word).</summary>
    private static readonly HashSet<string> RawCommon = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456", "1234567", "12345678", "123456789", "1234567890", "12345",
        "1234", "111111", "000000", "123123", "123321", "654321", "666666",
        "555555", "112233", "121212", "789456", "159753", "147258369",
        "password1", "password123", "passw0rd", "p@ssw0rd", "p@ssword",
        "qwerty123", "qwerty1", "qwe123", "123qwe", "1q2w3e", "1q2w3e4r",
        "1q2w3e4r5t", "qwer1234", "qwertyuiop", "zxcvbnm", "asdfghjkl",
        "abc123", "abcd1234", "a1b2c3", "admin123", "admin1", "root123",
        "welcome1", "welcome123", "letmein1", "iloveyou1", "secret123",
        "changeme", "trustno1", "login123", "test123", "pass123",
    };

    /// <summary>Dictionary base words checked against the leet-folded, letters-only
    /// form of the password (so "Passw0rd!" → "password" is rejected).</summary>
    private static readonly HashSet<string> BaseWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "qwerty", "qwertyuiop", "azerty", "welcome",
        "admin", "administrator", "letmein", "login", "secret", "master",
        "dragon", "monkey", "sunshine", "princess", "football", "baseball",
        "iloveyou", "superman", "batman", "starwars", "whatever", "freedom",
        "shadow", "michael", "jennifer", "hunter", "ashley", "soccer",
        "computer", "internet", "samsung", "google", "trustno", "abcdef",
        "abcdefg", "default", "changeme", "access", "flower", "charlie",
    };
}
