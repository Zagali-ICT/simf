using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The account-creation use cases — sign-up, email verification and code
/// resend (SIMF-API-001 section 12.4, SIMF-FDS-001).
/// </summary>
public interface IRegistrationService
{
    Task<SignUpResponse> SignUpAsync(
        SignUpRequest request,
        CancellationToken cancellationToken = default);

    Task<VerifyEmailResponse> VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default);

    Task<ResendCodeResponse> ResendCodeAsync(
        ResendCodeRequest request,
        CancellationToken cancellationToken = default);
}
