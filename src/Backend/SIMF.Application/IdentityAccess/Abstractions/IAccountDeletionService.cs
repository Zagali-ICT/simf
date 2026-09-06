using SIMF.Contracts.Account;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Self-service account deletion for the signed-in user
/// (<c>DELETE /api/v1/app/account</c>).
/// </summary>
/// <remarks>
/// <para>Google Play requires any app offering account creation to also offer
/// account deletion, in-app and via a public URL. This is the in-app half.</para>
/// <para><b>Anonymise, never row-delete.</b> Six App-side entities hold a real
/// FK onto <c>UserProfiles</c> with <c>DeleteBehavior.Restrict</c> — GateScan,
/// HallAttendance, Invitation, Speaker, SeatReservation and
/// ExhibitorVisitorScan — so removing the row throws. The profile is therefore
/// scrubbed in place and withdrawn from admission, which is also what the
/// codebase already means by "delete": nothing anywhere hard-deletes a
/// SimfUser or a UserProfile.</para>
/// <para>The immutable audit snapshots survive deliberately. OperationLog,
/// RowAudit and GateScan capture the actor's name at write time precisely so
/// the trail stays self-contained across the two separated databases, and
/// erasure does not reach them.</para>
/// </remarks>
public interface IAccountDeletionService
{
    /// <summary>
    /// Erases the caller's personal data, withdraws admission, revokes every
    /// credential, and disables the account. Idempotent: a second call on an
    /// already-deleted account succeeds without changing anything, so a client
    /// that retries after a half-landed cross-database write completes it.
    /// </summary>
    /// <param name="userId">The signed-in caller. Never an admin acting on someone else.</param>
    /// <param name="code">The emailed confirmation code from
    /// <see cref="SendDeletionCodeAsync"/>. Ignored when the gate is configured
    /// off; otherwise a missing or wrong value refuses the erasure.</param>
    Task DeleteOwnAccountAsync(
        Guid userId, string? code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Emails the caller a one-time code confirming they mean to erase their own
    /// account, and returns the masked address it went to.
    /// </summary>
    /// <remarks>
    /// <para>Deletion is irreversible, so it earns the same second factor that
    /// enrolling a credential does: an unlocked phone alone must not be able to
    /// destroy an account.</para>
    /// <para>Unlike the biometric step-up this mirrors, it is deliberately open
    /// to a PENDING, REJECTED or DISABLED holder. Those are exactly the people
    /// the deletion endpoint exists for, and gating the code behind an approved
    /// account would leave them able to ask for erasure and never complete
    /// it.</para>
    /// </remarks>
    Task<SendAccountDeletionCodeResponse> SendDeletionCodeAsync(
        Guid userId, CancellationToken cancellationToken = default);
}
