using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The authenticator-app enrolment use cases (myComment item #11, decision D-035).
/// A signed-in user generates a secret, scans the QR with an authenticator app
/// and confirms a first code; on success the account flips to
/// <c>TwoFactorEnabled = true</c>. A user can also turn 2FA off by providing a
/// current authenticator code.
/// </summary>
public interface ITotpEnrollmentService
{
    /// <summary>
    /// Generates a fresh TOTP secret for the account and stages it pending
    /// confirmation. Returns the secret, the <c>otpauth://</c> URI and an SVG
    /// QR code to render in the page.
    /// </summary>
    Task<TotpSetupResponse> SetupAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the user's first code against the staged secret. On success the
    /// staged secret becomes the active one and <c>TwoFactorEnabled</c> is
    /// turned on.
    /// </summary>
    Task<TotpConfirmResponse> ConfirmAsync(
        Guid userId,
        TotpConfirmRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns 2FA off for the account after verifying a current authenticator
    /// code. The active secret is removed so a re-enrolment starts cleanly.
    /// </summary>
    Task<TotpDisableResponse> DisableAsync(
        Guid userId,
        TotpDisableRequest request,
        CancellationToken cancellationToken = default);
}
