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
/// the trail stays self-contained (D-157), and erasure does not reach them.</para>
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
    Task DeleteOwnAccountAsync(Guid userId, CancellationToken cancellationToken = default);
}
