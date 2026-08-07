using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The authenticator-app enrolment use cases.
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

    /// <summary>
    /// Returns the QR + <c>otpauth://</c> URI for the user's CURRENT
    /// (already-active) authenticator secret. Does NOT rotate the secret —
    /// the same QR is returned on every call. Used for the
    /// <c>/account/totp-pairing</c> CP page so an admin whose authenticator
    /// device was lost (or whose seeded secret was never paired) can
    /// re-scan without resetting to a new secret. Returns <c>null</c> when
    /// the account has no active authenticator secret yet (the caller
    /// should send the user through <see cref="SetupAsync"/> in that case).
    /// </summary>
    Task<TotpSetupResponse?> GetCurrentPairingAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a code against the user's CURRENT active
    /// authenticator secret without mutating state — no replay-guard
    /// update, no flag change, no audit row. Lets the
    /// <c>/account/totp-pairing</c> page confirm a scan succeeded; the
    /// same code can immediately be used for sign-in afterwards. Returns
    /// <c>true</c> when the code is valid for the current time-step (or
    /// the adjacent ±1 windows). Returns <c>false</c> when the code is
    /// wrong OR when the account has no active secret (UI should route
    /// the user to <see cref="SetupAsync"/> in the latter case).
    /// </summary>
    Task<bool> VerifyPairingAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);
}
