using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// #24 — self-service change of a signed-in user's login email. Two steps that
/// mirror the biometric-enrol step-up (<c>DeviceKeyService.IssueEnrolStepUpAsync</c>):
/// issue a one-time code to the NEW address, then confirm it. The confirm
/// re-checks uniqueness, moves the login email, marks it confirmed, rolls the
/// security stamp and revokes every session so a stale token cannot survive the
/// identity change (the same protection the admin edit path applies in
/// <c>AdminAccountService.UpdateAccountAsync</c>).
/// </summary>
public interface IEmailChangeService
{
    /// <summary>
    /// Emails a verification code to <paramref name="request"/>'s new address for
    /// the signed-in <paramref name="userId"/>. Throws 400 when the new address is
    /// the current one, 409 when it already belongs to another account, 429 when
    /// the per-account request cap is reached.
    /// </summary>
    Task<SendEmailChangeOtpResponse> SendChangeOtpAsync(
        Guid userId, SendEmailChangeOtpRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the code emailed to the new address and, on success, moves the
    /// login email, rolls the security stamp and revokes sessions. Throws 400 for
    /// a missing / expired / incorrect code and 409 if the address was taken in
    /// the meantime.
    /// </summary>
    Task<ConfirmEmailChangeResponse> ConfirmChangeAsync(
        Guid userId, ConfirmEmailChangeRequest request,
        CancellationToken cancellationToken = default);
}
