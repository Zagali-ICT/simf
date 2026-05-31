using SIMF.Contracts.Authentication;

namespace SIMF.Application.IdentityAccess;

/// <summary>
/// The password-recovery use cases — forgot-password, reset-password (with the
/// emailed code) and change-password (SIMF-API-001 section 12.7).
/// </summary>
public interface IPasswordService
{
    Task<ForgotPasswordResponse> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ResetPasswordResponse> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ChangePasswordResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// D-206: completes a forced password change against the single-use ticket
    /// the sign-in step issued for a Control Panel account holding a
    /// seeded/admin-rotated credential. The ticket authorises the change (the
    /// current password was proven at sign-in), so it is not re-collected.
    /// Clears the forced-change flag and ends every session on success.
    /// </summary>
    Task<CompletePasswordChangeResponse> CompletePasswordChangeAsync(
        CompletePasswordChangeRequest request,
        CancellationToken cancellationToken = default);
}
