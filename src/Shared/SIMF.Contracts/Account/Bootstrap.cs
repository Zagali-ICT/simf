using SIMF.Contracts.Authentication;

namespace SIMF.Contracts.Account;

/// <summary>
/// The on-login bundle the Flutter app fetches once and caches (App Home /
/// Screen 13, D9 — D-249): the signed-in user (identity + app-role +
/// registration status), the unread-notification badge count, and the server
/// clock for client skew correction. One round-trip instead of several — an
/// additive read-only aggregate of existing reads (no schema change).
/// <para>Available to any signed-in account, including not-yet-approved ones —
/// the app caches the privilege + the routing decision on login. Richer,
/// screen-specific data (the full profile, the agenda, …) is fetched lazily per
/// screen, not bundled here.</para>
/// </summary>
public sealed record AppBootstrap(
    CurrentUserResponse User,
    int UnreadNotificationCount,
    DateTimeOffset ServerTimeUtc);
