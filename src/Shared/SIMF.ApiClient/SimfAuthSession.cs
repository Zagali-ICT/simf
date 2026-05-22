using SIMF.Contracts.Authentication;

namespace SIMF.ApiClient;

/// <summary>
/// Holds the signed-in user's tokens for the lifetime of the Blazor circuit
/// (registered scoped). The tokens stay on the server and are never handed to
/// the browser.
///
/// Persisting the session across circuits — an authentication cookie and an
/// <c>AuthenticationStateProvider</c> — belongs with the authenticated
/// application shell. This increment delivers the sign-in flow and a landing
/// placeholder, so an in-circuit hold is enough and is documented as such.
/// </summary>
public sealed class SimfAuthSession
{
    /// <summary>The token pair from a completed sign-in; <c>null</c> when signed out.</summary>
    public AuthTokens? Tokens { get; private set; }

    /// <summary>The signed-in user, or <c>null</c> when signed out.</summary>
    public AuthUser? User => Tokens?.User;

    /// <summary>True once a sign-in has fully completed.</summary>
    public bool IsSignedIn => Tokens is not null;

    /// <summary>The email carried from the sign-in step into the second-factor step.</summary>
    public string? PendingEmail { get; set; }

    /// <summary>The Control Panel MFA ticket awaiting a TOTP code.</summary>
    public string? PendingMfaToken { get; set; }

    /// <summary>The visitor OTP ticket awaiting an emailed code.</summary>
    public string? PendingOtpToken { get; set; }

    /// <summary>Records a completed sign-in and clears any pending second-factor state.</summary>
    public void Complete(AuthTokens tokens)
    {
        Tokens = tokens;
        ClearPending();
    }

    /// <summary>Clears the session — used on sign-out.</summary>
    public void Clear()
    {
        Tokens = null;
        ClearPending();
    }

    private void ClearPending()
    {
        PendingEmail = null;
        PendingMfaToken = null;
        PendingOtpToken = null;
    }

    /// <summary>
    /// Masks an email for display — keeps the first and last character of the
    /// local part, e.g. <c>a••••z@example.com</c>. Used on the verification and
    /// reset screens so the address is recognisable without being exposed.
    /// </summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@', StringComparison.Ordinal);
        if (at <= 0)
        {
            return email;
        }

        var local = email[..at];
        var domain = email[at..];
        var masked = local.Length <= 2
            ? $"{local[0]}••••"
            : $"{local[0]}••••{local[^1]}";
        return masked + domain;
    }
}
