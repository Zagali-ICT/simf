using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Part B — badge-QR sign-in / activation. Any badge holder can scan the QR on
/// their printed badge at the app login. An account that already has a password
/// continues to the normal password + OTP sign-in; a passwordless account
/// (admin-created / walk-in, approved, never activated) goes through a
/// set-password flow gated by an emailed verification code. Pre-authentication,
/// like forgot-password — the caller holds no token yet.
/// </summary>
public interface IBadgeAuthService
{
    /// <summary>Resolves a scanned badge QR to the branch the app should take
    /// (found? has a password? needs an email?). Never throws for an unknown QR
    /// — it returns <see cref="ResolveBadgeResponse.Found"/> = false.</summary>
    Task<ResolveBadgeResponse> ResolveAsync(
        ResolveBadgeRequest request, CancellationToken cancellationToken = default);

    /// <summary>Completes sign-in for a returning holder whose account already has
    /// a password: the scanned QR selects the account, then the supplied password
    /// (plus any 2FA / lockout) runs through the normal sign-in pipeline. Returns
    /// the standard <see cref="SignInResponse"/> (issued tokens or the 2FA
    /// challenge). An unknown QR — or a resolved account with the wrong password —
    /// throws the same generic invalid-credentials error, so the public badge
    /// never reveals whether the QR was valid and never bypasses the
    /// password.</summary>
    Task<SignInResponse> SignInAsync(
        BadgeSignInRequest request, CancellationToken cancellationToken = default);

    /// <summary>Issues the email verification code that gates the first-password
    /// step for a passwordless account. Sends to the account's on-file email, or
    /// to the supplied email when the account has none. Throws if the QR is
    /// unknown or the account already has a password.</summary>
    Task<BadgeActivationStartResponse> StartActivationAsync(
        BadgeActivationStartRequest request, CancellationToken cancellationToken = default);

    /// <summary>Verifies the emailed code and sets the account's first password
    /// (confirming/attaching the email). Throws if the QR is unknown, the account
    /// already has a password, or the code is invalid / expired / over the
    /// attempt cap.</summary>
    Task<BadgeActivationCompleteResponse> CompleteActivationAsync(
        BadgeActivationCompleteRequest request, CancellationToken cancellationToken = default);
}
