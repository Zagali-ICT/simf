using System.Globalization;

namespace SIMF.Application.Email;

/// <summary>Builds the token bag a one-time-code email hands to
/// <see cref="IEmailTemplateResolver"/>. Centralises the single shape every code
/// email shares (<c>{Code}</c> + <c>{ExpiryMinutes}</c>) so the culture-invariant
/// minute formatting lives in one place instead of being repeated at each builder.
/// </summary>
public static class EmailTokens
{
    /// <summary>The <c>{Code}</c> + <c>{ExpiryMinutes}</c> pair, keyed
    /// case-insensitively.</summary>
    public static IReadOnlyDictionary<string, string> ForCode(string code, TimeSpan expiry) =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Code"] = code,
            ["ExpiryMinutes"] = ((int)expiry.TotalMinutes).ToString(CultureInfo.InvariantCulture),
        };
}
