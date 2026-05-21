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
}
