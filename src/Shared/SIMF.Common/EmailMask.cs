namespace SIMF.Common;

/// <summary>
/// Masks an email for a "we sent a code to t***@x.com" line — keeps the first
/// character of the local part and the full domain, stars the rest. Never returns
/// the full address. Shared by the flows that email a one-time code to an address
/// and then tell the user (masked) where it went (biometric step-up, change-email).
/// </summary>
public static class EmailMask
{
    public static string Mask(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "***";
        }
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }
        return $"{email[..1]}***{email[at..]}";
    }
}
