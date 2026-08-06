using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// TOTP / authenticator-token + 2FA-toggle surface for
/// the <see cref="SimfUser"/> aggregate. Split out of the 22-method
/// <see cref="IUserAccountRepository"/> so the TOTP enrolment flow
/// and the admin 2FA-reset endpoint depend only on the methods they
/// actually use.
/// </summary>
public interface IUserTwoFactorStore
{
    Task<string?> GetAuthenticatorKeyAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task<UserOperationResult> SetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, string tokenValue, CancellationToken cancellationToken = default);

    Task<string?> GetAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default);

    Task<UserOperationResult> RemoveAuthenticationTokenAsync(SimfUser user, string loginProvider, string tokenName, CancellationToken cancellationToken = default);

    Task<UserOperationResult> SetTwoFactorEnabledAsync(SimfUser user, bool enabled, CancellationToken cancellationToken = default);
}
