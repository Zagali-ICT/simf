namespace SIMF.Application.IdentityAccess;

/// <summary>
/// Mints, verifies and consumes the single-use recovery codes that a user can
/// submit when they've lost access to their authenticator app (D-040). Codes
/// are returned plaintext only at issuance — never again.
/// </summary>
public interface IRecoveryCodeService
{
    /// <summary>
    /// Generates a fresh batch (10 codes), revokes any previously active batch,
    /// persists the hashes, and returns the plaintext codes for the caller to
    /// show the user once.
    /// </summary>
    Task<IReadOnlyList<string>> GenerateAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>The number of active (un-consumed) codes the user still has.</summary>
    Task<int> CountActiveAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the supplied code against the user's active codes. On a match
    /// the code is consumed and the method returns <c>true</c>; on no match
    /// the method returns <c>false</c> (the caller decides whether to count
    /// the attempt against lockout).
    /// </summary>
    Task<bool> VerifyAndConsumeAsync(
        Guid userId,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>Removes every code for the user — on disable or via the seeder.</summary>
    Task RevokeAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
