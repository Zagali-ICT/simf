using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// R3.5 — D-094: password + security-stamp credential surface for the
/// <see cref="SimfUser"/> aggregate. Split out of the 22-method
/// <see cref="IUserAccountRepository"/> so a sign-in service does not also
/// have to inject the role / 2FA seams it never uses.
/// </summary>
public interface IUserCredentialStore
{
    Task<bool> CheckPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    Task<UserOperationResult> AddPasswordAsync(SimfUser user, string password, CancellationToken cancellationToken = default);

    Task<UserOperationResult> RemovePasswordAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<UserOperationResult> ChangePasswordAsync(SimfUser user, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>A7-20 (NCA) — true when <paramref name="providedPassword"/> matches
    /// the stored Identity password hash <paramref name="hashedPassword"/>. Used to
    /// check a new password against the account's password history. No DB I/O.</summary>
    bool PasswordHashMatches(SimfUser user, string hashedPassword, string providedPassword);

    Task UpdateSecurityStampAsync(SimfUser user, CancellationToken cancellationToken = default);
}
