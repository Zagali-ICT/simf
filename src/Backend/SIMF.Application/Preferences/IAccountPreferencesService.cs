using SIMF.Contracts.Account;

namespace SIMF.Application.Preferences;

/// <summary>
/// `accessibility-server-sync` — reads and writes the signed-in account's five
/// accessibility choices (<c>GET</c> / <c>PUT /app/account/preferences</c>).
/// The values live on the user's <c>UserProfile</c> row (which already carries
/// the bare <c>UserId</c> — D-157, no cross-DB FK), so they follow the user to a
/// second device and survive a reinstall. The device prefs stay as the app's
/// offline cache.
/// </summary>
public interface IAccountPreferencesService
{
    /// <summary>The account's stored choices. An account that has never saved
    /// any — and one with no profile row at all — reads back
    /// <see cref="AccountPreferences.Defaults"/> rather than a 404.</summary>
    Task<AccountPreferences> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="request"/> against the account and returns
    /// what was persisted. A full replace, so calling it twice with the same body
    /// is a no-op; an unknown <c>textSize</c> is rejected (bilingual 400) rather
    /// than coerced.</summary>
    Task<AccountPreferences> SaveMineAsync(
        Guid userId,
        UpdateAccountPreferencesRequest request,
        CancellationToken cancellationToken = default);
}
