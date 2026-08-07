using SIMF.Domain.IdentityAccess;

namespace SIMF.Application.IdentityAccess.Abstractions;

/// <summary>
/// Lockout and access-failure tracking for the
/// <see cref="SimfUser"/> aggregate. Split out of the 22-method
/// <see cref="IUserAccountRepository"/> so lockout-aware orchestrators
/// (sign-in flow, admin unlock endpoint) advertise their dependency
/// at the type level.
/// </summary>
public interface IUserLockoutTracker
{
    Task<bool> IsLockedOutAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task AccessFailedAsync(SimfUser user, CancellationToken cancellationToken = default);

    Task ResetAccessFailedCountAsync(SimfUser user, CancellationToken cancellationToken = default);
}
