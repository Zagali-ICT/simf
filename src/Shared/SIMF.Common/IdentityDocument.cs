using System.Text.RegularExpressions;

namespace SIMF.Common;

/// <summary>
/// The one place that answers "is this a well-formed identity document
/// number".
///
/// <para>The rule used to live only in <c>UpsertUserProfileRequestValidator</c>,
/// with the walk-in desk validator calling into it. Both sit in
/// <c>SIMF.Api</c>, which <c>SIMF.Infrastructure</c> cannot reference — and the
/// offline badge batch runs in Infrastructure and calls
/// <c>RegisterOnSiteAsync</c> directly, so no FastEndpoints validator ever
/// executes on it. Without a shared home the only way to check an uploaded badge
/// row would have been a third copy of the rule. Same reason
/// <see cref="SaudiPlate"/> and <see cref="MobileNumber"/> already live here.</para>
///
/// <para><b>Why the checksum matters more than the shape.</b> The
/// duplicate-identity guard keys off a blind-index HMAC of these numbers. A
/// mistyped id is still unique, so it hashes to a value that matches nothing,
/// the guard never fires, and the same person collects a second badge at another
/// desk. The Luhn check digit is what catches the typo.</para>
/// </summary>
public static class IdentityDocument
{
    /// <summary>Saudi national ID: 10 digits starting with 1. Explicit
    /// <c>[0-9]</c> rather than <c>\d</c>, which in .NET also matches
    /// Arabic-Indic digits — a number typed on an Arabic keyboard would pass the
    /// shape and then fail every comparison downstream.</summary>
    private static readonly Regex SaudiNationalIdShape =
        new("^1[0-9]{9}$", RegexOptions.Compiled);

    /// <summary>Iqama (residency permit): 10 digits starting with 2.</summary>
    private static readonly Regex IqamaShape =
        new("^2[0-9]{9}$", RegexOptions.Compiled);

    /// <summary>
    /// The longest passport / other-document number the desk paths accept,
    /// matching <c>AdminWalkInRegistrationRequestValidator</c> exactly.
    ///
    /// <para>A LENGTH CAP AND NOTHING ELSE, deliberately. An earlier cut used
    /// the self-service form's <c>[A-Za-z0-9]{6,9}</c> here and it
    /// was wrong twice over. The desk classifier files ANY document that is not
    /// a 10-digit Saudi-shaped number into the passport field, so that shape
    /// also had to cover a Kuwaiti Civil ID (12 digits), a Qatari QID (11), an
    /// Emirates ID (15) and any passport carrying a space or hyphen — all
    /// ordinary at an international maritime forum. And the walk-in desk, which
    /// is the ONLINE twin of this path, accepts all of them. Rejecting offline
    /// what the same operator can register online would have stranded printed
    /// badges with no truthful value that could clear them, which is precisely
    /// the failure the correction path exists to prevent.</para>
    ///
    /// <para>There is no checksum to check: passport numbers do not carry one.
    /// The national ID and Iqama rules below are strict because those documents
    /// have a defined shape AND a check digit — that is what makes catching a
    /// typo possible at all.</para>
    /// </summary>
    public const int OtherDocumentMaxLength = 20;

    /// <summary>True when the value is a Saudi national ID — the 10-digit shape
    /// AND the Luhn check digit. Blank is NOT valid here; the caller owns the
    /// required-or-optional decision.</summary>
    public static bool IsSaudiNationalId(string? value) =>
        value is not null && SaudiNationalIdShape.IsMatch(value) && IsValidLuhn(value);

    /// <summary>True when the value is an Iqama number — the 10-digit shape AND
    /// the Luhn check digit.</summary>
    public static bool IsIqamaNumber(string? value) =>
        value is not null && IqamaShape.IsMatch(value) && IsValidLuhn(value);

    /// <summary>True when the value is an acceptable passport / other-document
    /// number. Length only — see <see cref="OtherDocumentMaxLength"/>.</summary>
    public static bool IsOtherDocumentNumber(string? value) =>
        value is { Length: > 0 } && value.Length <= OtherDocumentMaxLength;

    /// <summary>
    /// Standard Luhn mod-10 over all digits, the last being the check
    /// digit. Saudi national ids and Iqama numbers are Luhn-valid; this is the
    /// real check on top of the prefix/length shape.
    ///
    /// <para>Returns true for an empty string, which is the behaviour every
    /// existing caller already guards with its own <c>IsNullOrEmpty</c> check.
    /// Preserved deliberately on the move so no caller changes meaning.</para>
    /// </summary>
    public static bool IsValidLuhn(string number)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var i = number.Length - 1; i >= 0; i--)
        {
            if (!char.IsAsciiDigit(number[i])) { return false; }
            var value = number[i] - '0';
            if (doubleDigit)
            {
                value *= 2;
                if (value > 9) { value -= 9; }
            }
            sum += value;
            doubleDigit = !doubleDigit;
        }
        return sum % 10 == 0;
    }
}
