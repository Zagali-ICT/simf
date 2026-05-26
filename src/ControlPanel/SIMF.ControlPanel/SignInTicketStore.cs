// Tests: SIMF.ControlPanel.Tests/SignInTicketStoreTests.cs
using Microsoft.Extensions.Caching.Memory;
using SIMF.Contracts.Authentication;

using SIMF.Common.Enums;

namespace SIMF.ControlPanel;

/// <summary>
/// A short-lived, single-use server-side hand-off for a completed sign-in.
///
/// The authentication cookie can only be written in an HTTP request context,
/// not from an interactive Blazor circuit. So the verification page stashes the
/// token pair here, keyed by a random reference, and full-page-navigates to the
/// completion endpoint, which redeems the reference and issues the cookie.
///
/// <para>P11 — D-052 extends the ticket with the optional
/// <see cref="AccountStateInfo"/> from the API's sign-in response so the
/// completion endpoint can write the bilingual rejection reason into the
/// cookie claims — the state-banner pages read the cookie, never the API.</para>
/// </summary>
public sealed class SignInTicketStore(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>Stashes a completed sign-in and returns its one-time reference.</summary>
    public string Stash(AuthTokens tokens, AccountStateInfo? accountState = null)
    {
        var reference = Guid.NewGuid().ToString("N");
        cache.Set(CacheKey(reference), new SignInTicketPayload(tokens, accountState), Lifetime);
        return reference;
    }

    /// <summary>Redeems a reference once; returns <c>null</c> if unknown or already used.</summary>
    public SignInTicketPayload? Redeem(string reference)
    {
        var key = CacheKey(reference);
        if (cache.TryGetValue(key, out SignInTicketPayload? payload))
        {
            cache.Remove(key);
            return payload;
        }
        return null;
    }

    private static string CacheKey(string reference) => $"simf:signin-ticket:{reference}";
}

/// <summary>The payload stashed under one sign-in ticket (P11 — D-052).</summary>
public sealed record SignInTicketPayload(
    AuthTokens Tokens,
    AccountStateInfo? AccountState);
