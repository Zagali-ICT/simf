// Tests: SIMF.Web.Tests (todo). Mirrors SIMF.ControlPanel.SignInTicketStore.
using Microsoft.Extensions.Caching.Memory;
using SIMF.Contracts.Authentication;

namespace SIMF.Web;

/// <summary>
/// A short-lived, single-use server-side hand-off for a completed sign-in
/// on the Website (decision D-046 c — mirrors the CP's
/// <c>SignInTicketStore</c>). The authentication cookie can only be
/// written in an HTTP request context, not from an interactive Blazor
/// circuit, so the verification page stashes the token pair here, keyed
/// by a random reference, and full-page-navigates to the completion
/// endpoint which redeems the reference and issues the cookie.
/// </summary>
public sealed class SignInTicketStore(IMemoryCache cache)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    public string Stash(AuthTokens tokens)
    {
        var reference = Guid.NewGuid().ToString("N");
        cache.Set(CacheKey(reference), tokens, Lifetime);
        return reference;
    }

    public AuthTokens? Redeem(string reference)
    {
        var key = CacheKey(reference);
        if (cache.TryGetValue(key, out AuthTokens? tokens))
        {
            cache.Remove(key);
            return tokens;
        }
        return null;
    }

    private static string CacheKey(string reference) => $"simf-web:signin-ticket:{reference}";
}
