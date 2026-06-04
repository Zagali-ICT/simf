using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The sign-in use cases — the password step and the two second-factor steps
/// (SIMF-API-001 section 12.4, SIMF-FDS-001 section 5).
/// </summary>
public interface ISignInService
{
    Task<SignInResponse> SignInAsync(
        SignInRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthTokens> VerifyTotpAsync(
        VerifyTotpRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthTokens> VerifyOtpAsync(
        VerifyOtpRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes sign-in using a single-use recovery code instead of a TOTP
    /// (decision D-040). The MFA token is the same ticket the password step
    /// issued.
    /// </summary>
    Task<AuthTokens> VerifyRecoveryCodeAsync(
        VerifyRecoveryCodeRequest request,
        CancellationToken cancellationToken = default);
}
